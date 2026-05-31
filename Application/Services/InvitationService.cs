using System.Security.Cryptography;
using Application.DTOs.RequestDTOs.Invitation;
using Application.DTOs.ResponseDTOs.Invitation;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
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
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        public InvitationService(
            IUnitOfWork unitOfWork,
            INotificationService notification,
            ICurrentUserService currentUser,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _notification = notification;
            _currentUser = currentUser;
            _mapper = mapper;
        }

        public async Task<InvitationResponseDTO> InviteAsync(InviteRequestDTO dto)
        {
            var inviter = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(dto.ProjectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var group = await _unitOfWork.Repository<Group>().GetByIdAsync(dto.InvitedGroupId)
                ?? throw new ApiExceptionResponse("Group not found.", 404);

            _ = await _unitOfWork.Repository<Account>().GetByIdAsync(dto.InvitedAccountId)
                ?? throw new ApiExceptionResponse("Invited account not found.", 404);

            // Chống mời lại nếu đã là member group này
            var alreadyMember = (await _unitOfWork.Repository<GroupMember>().GetAllAsync())
                .Any(gm => gm.GroupId == dto.InvitedGroupId && gm.AccountId == dto.InvitedAccountId);
            if (alreadyMember)
                throw new ApiExceptionResponse("Account is already a member of this group.", 409);

            // Chống mời trùng khi đang Pending
            var hasPending = (await _unitOfWork.Repository<ProjectInvitation>().GetAllAsync())
                .Any(i => i.ProjectId == dto.ProjectId
                       && i.InvitedAccountId == dto.InvitedAccountId
                       && i.InvitedGroupId == dto.InvitedGroupId
                       && i.Status == InvitationStatus.Pending);
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

            await _notification.NotifyAsync(
                dto.InvitedAccountId,
                $"Bạn được mời vào nhóm \"{group.Name}\" của dự án \"{project.ProjectName}\"",
                senderName: _currentUser.UserName ?? "System",
                linkType: "ProjectInvitation",
                linkId: invitation.Id.ToString());

            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        public async Task<InvitationResponseDTO> AcceptAsync(Guid invitationId)
        {
            var accountId = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var invitation = await EnsureActiveOwnedAsync(invitationId, accountId);

            if (!invitation.InvitedGroupId.HasValue)
                throw new ApiExceptionResponse("Invitation is missing target group.", 400);

            var groupId = invitation.InvitedGroupId.Value;
            var projectId = invitation.ProjectId;

            // 1) Tạo GroupMember nếu chưa có (idempotent)
            var existsMember = (await _unitOfWork.Repository<GroupMember>().GetAllAsync())
                .Any(gm => gm.GroupId == groupId && gm.AccountId == accountId);

            if (!existsMember)
            {
                await _unitOfWork.Repository<GroupMember>().CreateAsync(new GroupMember
                {
                    Id = Guid.NewGuid(),
                    GroupId = groupId,
                    AccountId = accountId,
                    Role = invitation.Role,
                    JoinedAt = DateTime.UtcNow
                });
            }

            // 2) Auto-link Group vào Project (idempotent) — đỡ phải nhớ bước riêng
            var existsParticipant = (await _unitOfWork.Repository<ProjectParticipant>().GetAllAsync())
                .Any(p => p.ProjectId == projectId && p.GroupId == groupId);

            if (!existsParticipant)
            {
                await _unitOfWork.Repository<ProjectParticipant>().CreateAsync(new ProjectParticipant
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    GroupId = groupId,
                    Role = ProjectParticipantRole.Member,
                    JoinedAt = DateTime.UtcNow
                });
            }

            invitation.Status = InvitationStatus.Accepted;
            invitation.RespondedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            // Báo lại cho PM đã mời
            if (invitation.InvitedByAccountId.HasValue)
            {
                await _notification.NotifyAsync(
                    invitation.InvitedByAccountId.Value,
                    $"{_currentUser.UserName ?? "Người dùng"} đã chấp nhận lời mời vào nhóm.",
                    senderName: _currentUser.UserName ?? "System",
                    linkType: "ProjectInvitation",
                    linkId: invitation.Id.ToString());
            }

            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        public async Task<InvitationResponseDTO> RejectAsync(Guid invitationId)
        {
            var accountId = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var invitation = await EnsureActiveOwnedAsync(invitationId, accountId);

            invitation.Status = InvitationStatus.Rejected;
            invitation.RespondedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            if (invitation.InvitedByAccountId.HasValue)
            {
                await _notification.NotifyAsync(
                    invitation.InvitedByAccountId.Value,
                    $"{_currentUser.UserName ?? "Người dùng"} đã từ chối lời mời vào nhóm.",
                    senderName: _currentUser.UserName ?? "System",
                    linkType: "ProjectInvitation",
                    linkId: invitation.Id.ToString());
            }

            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        public async Task<IEnumerable<MyInvitationDTO>> GetMyPendingAsync()
        {
            var accountId = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var invitations = (await _unitOfWork.Repository<ProjectInvitation>().GetAllAsync())
                .Where(i => i.InvitedAccountId == accountId && i.Status == InvitationStatus.Pending)
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
