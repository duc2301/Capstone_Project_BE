using System.Security.Cryptography;
using Application.DTOs.RequestDTOs.Invitation;
using Application.DTOs.ResponseDTOs.Invitation;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Group;
using Domain.Enum.Invitation;
using Domain.Enum.Project;

namespace Application.Services
{
    // Luồng nghiệp vụ:
    //   PM tạo Group -> PM gọi Invite(account, group, role)
    //   -> Service ghi ProjectInvitation + push Notification cho account đó
    //   -> Account login, GET /me thấy invitation -> POST /{id}/accept hoặc /{id}/reject
    //   -> Accept: tạo GroupMember(Role) + auto-tạo ProjectParticipant nếu Group chưa nằm trong Project
    public class InvitationService : IInvitationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notification;
        private readonly IFolderBootstrapService _folderBootstrap;
        private readonly IMapper _mapper;

        public InvitationService(
            IUnitOfWork unitOfWork,
            INotificationService notification,
            IFolderBootstrapService folderBootstrap,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _notification = notification;
            _folderBootstrap = folderBootstrap;
            _mapper = mapper;
        }

        public async Task<InvitationResponseDTO> InviteAsync(InviteRequestDTO dto, Guid inviter, string? inviterName)
        {
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(dto.ProjectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var group = await _unitOfWork.Repository<Group>().GetByIdAsync(dto.InvitedGroupId)
                ?? throw new ApiExceptionResponse("Group not found.", 404);

            _ = await _unitOfWork.Repository<Account>().GetByIdAsync(dto.InvitedAccountId)
                ?? throw new ApiExceptionResponse("Invited account not found.", 404);

            var groupMembers = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.GroupId == dto.InvitedGroupId))
                .ToList();
            var existingMember = groupMembers.FirstOrDefault(gm => gm.AccountId == dto.InvitedAccountId);
            if (existingMember != null && existingMember.Status != GroupMemberStatus.Left)
                throw new ApiExceptionResponse("Account is already a member of this group.", 409);

            // Chặn sớm: không mời thêm Leader nếu nhóm đã có Leader đang hoạt động (Active).
            if (dto.Role == GroupMemberRole.Leader
                && groupMembers.Any(gm => gm.Role == GroupMemberRole.Leader && gm.Status == GroupMemberStatus.Active))
                throw new ApiExceptionResponse("Nhóm đã có Trưởng nhóm (Leader). Không thể mời thêm Leader.", 409);

            // Chống mời trùng khi đang Pending
            var hasPending = (await _unitOfWork.Repository<ProjectInvitation>()
                    .FindAsync(i => i.ProjectId == dto.ProjectId
                       && i.InvitedAccountId == dto.InvitedAccountId
                       && i.InvitedGroupId == dto.InvitedGroupId
                       && i.Status == InvitationStatus.Pending))
                .Any();
            if (hasPending)
                throw new ApiExceptionResponse("A pending invitation already exists for this account/group.", 409);

            var invitation = _mapper.Map<ProjectInvitation>(dto);
            invitation.Id = Guid.NewGuid();
            invitation.InvitedByAccountId = inviter;
            invitation.Token = GenerateToken();
            invitation.Status = InvitationStatus.Pending;
            invitation.CreatedAt = DateTime.UtcNow;
            invitation.ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpireDays > 0 ? dto.ExpireDays : 7);

            await _unitOfWork.Repository<ProjectInvitation>().CreateAsync(invitation);

            await _unitOfWork.CommitAsync();

            var displayName = inviterName ?? "Một người dùng";
            await _notification.NotifyAsync(
                dto.InvitedAccountId,
                $"{displayName} đã mời bạn vào nhóm \"{group.Name}\" với vai trò {RoleLabel(dto.Role)} " +
                $"thuộc dự án \"{project.ProjectName}\".",
                senderName: displayName,
                linkType: "ProjectInvitation",
                linkId: invitation.Id.ToString());

            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        public async Task<InvitationResponseDTO> AcceptAsync(Guid invitationId, Guid accountId, string? actorName)
        {
            var invitation = await EnsureActiveOwnedAsync(invitationId, accountId);

            if (!invitation.InvitedGroupId.HasValue)
                throw new ApiExceptionResponse("Invitation is missing target group.", 400);

            var groupId = invitation.InvitedGroupId.Value;
            var projectId = invitation.ProjectId;

            var groupMembers = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.GroupId == groupId))
                .ToList();
            var member = groupMembers.FirstOrDefault(gm => gm.AccountId == accountId);

            // Chặn nhận vai trò Leader khi nhóm đã có Leader Active khác.
            if (invitation.Role == GroupMemberRole.Leader
                && groupMembers.Any(gm => gm.Role == GroupMemberRole.Leader
                                       && gm.Status == GroupMemberStatus.Active
                                       && gm.AccountId != accountId))
            {
                throw new ApiExceptionResponse(
                    "Nhóm đã có Trưởng nhóm (Leader). Không thể gia nhập với vai trò Leader.", 409);
            }

            if (member != null)
            {
                member.Role = invitation.Role;
                member.Status = GroupMemberStatus.Active;
                member.JoinedAt = DateTime.UtcNow;
            }
            else
            {
                await _unitOfWork.Repository<GroupMember>().CreateAsync(new GroupMember
                {
                    Id = Guid.NewGuid(),
                    GroupId = groupId,
                    AccountId = accountId,
                    Role = invitation.Role,
                    Status = GroupMemberStatus.Active,
                    JoinedAt = DateTime.UtcNow
                });
            }

            var participant = (await _unitOfWork.Repository<ProjectParticipant>()
                    .FindAsync(p => p.ProjectId == projectId && p.GroupId == groupId))
                .FirstOrDefault();
            var isNewParticipant = participant == null;

            if (participant == null)
            {
                await _unitOfWork.Repository<ProjectParticipant>().CreateAsync(new ProjectParticipant
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    GroupId = groupId,
                    Role = ProjectParticipantRole.Member,
                    Status = ProjectParticipantStatus.Active,
                    JoinedAt = DateTime.UtcNow
                });
            }
            else if (participant.Status != ProjectParticipantStatus.Active)
            {
                participant.Status = ProjectParticipantStatus.Active;
            }

            invitation.Status = InvitationStatus.Accepted;
            invitation.RespondedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            // Group lần đầu vào dự án -> dựng "ô" thư mục CDE cho bên này (idempotent).
            if (isNewParticipant)
                await _folderBootstrap.ScaffoldParticipantFoldersAsync(projectId, groupId);

            if (invitation.InvitedByAccountId.HasValue)
            {
                var group = await _unitOfWork.Repository<Group>().GetByIdAsync(groupId);
                var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId);
                await _notification.NotifyAsync(
                    invitation.InvitedByAccountId.Value,
                    $"{actorName ?? "Người dùng"} đã chấp nhận lời mời vào nhóm \"{group?.Name}\" " +
                    $"(vai trò {RoleLabel(invitation.Role)}) của dự án \"{project?.ProjectName}\".",
                    senderName: actorName ?? "System",
                    linkType: "ProjectInvitation",
                    linkId: invitation.Id.ToString());
            }

            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        public async Task<InvitationResponseDTO> RejectAsync(Guid invitationId, Guid accountId, string? actorName)
        {
            var invitation = await EnsureActiveOwnedAsync(invitationId, accountId);

            invitation.Status = InvitationStatus.Rejected;
            invitation.RespondedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            if (invitation.InvitedByAccountId.HasValue)
            {
                var group = invitation.InvitedGroupId.HasValue
                    ? await _unitOfWork.Repository<Group>().GetByIdAsync(invitation.InvitedGroupId.Value)
                    : null;
                var project = await _unitOfWork.Repository<Project>().GetByIdAsync(invitation.ProjectId);
                await _notification.NotifyAsync(
                    invitation.InvitedByAccountId.Value,
                    $"{actorName ?? "Người dùng"} đã từ chối lời mời vào nhóm \"{group?.Name}\" " +
                    $"của dự án \"{project?.ProjectName}\".",
                    senderName: actorName ?? "System",
                    linkType: "ProjectInvitation",
                    linkId: invitation.Id.ToString());
            }

            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        private static string RoleLabel(GroupMemberRole role)
            => role == GroupMemberRole.Leader ? "Trưởng nhóm" : "Thành viên";

        public async Task<IEnumerable<MyInvitationDTO>> GetMyPendingAsync(Guid accountId)
        {
            var invitations = (await _unitOfWork.Repository<ProjectInvitation>()
                    .FindAsync(i => i.InvitedAccountId == accountId && i.Status == InvitationStatus.Pending))
                .ToList();

            if (invitations.Count == 0)
                return Enumerable.Empty<MyInvitationDTO>();

            var projects = (await _unitOfWork.Repository<Project>().GetAllAsync())
                .ToDictionary(p => p.Id);
            var groups = (await _unitOfWork.Repository<Group>().GetAllAsync())
                .ToDictionary(g => g.Id);
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return invitations.Select(i => new MyInvitationDTO
            {
                Id = i.Id,
                ProjectId = i.ProjectId,
                ProjectName = projects.TryGetValue(i.ProjectId, out var p) ? p.ProjectName : "",
                InvitedGroupId = i.InvitedGroupId ?? Guid.Empty,
                GroupName = i.InvitedGroupId.HasValue && groups.TryGetValue(i.InvitedGroupId.Value, out var g) ? g.Name : "",
                Role = i.Role,
                InvitedByAccountId = i.InvitedByAccountId,
                InvitedByName = i.InvitedByAccountId.HasValue && accounts.TryGetValue(i.InvitedByAccountId.Value, out var a) ? a.UserName : null,
                Status = i.Status,
                ExpiresAt = i.ExpiresAt,
                CreatedAt = i.CreatedAt,
                Note = i.Note
            });
        }

        // Tìm invitation theo Id + check ownership + check active (Pending + chưa hết hạn).
        private async Task<ProjectInvitation> EnsureActiveOwnedAsync(Guid invitationId, Guid accountId)
        {
            var invitation = await _unitOfWork.Repository<ProjectInvitation>().GetByIdAsync(invitationId)
                ?? throw new ApiExceptionResponse("Invitation not found.", 404);

            if (invitation.InvitedAccountId.HasValue && invitation.InvitedAccountId.Value != accountId)
                throw new ApiExceptionResponse("This invitation is for a different account.", 403);

            if (invitation.Status != InvitationStatus.Pending)
                throw new ApiExceptionResponse($"Invitation already {invitation.Status}.", 409);

            if (invitation.ExpiresAt < DateTime.UtcNow)
            {
                invitation.Status = InvitationStatus.Expired;
                await _unitOfWork.CommitAsync();
                throw new ApiExceptionResponse("Invitation expired.", 410);
            }

            return invitation;
        }

        private static string GenerateToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
