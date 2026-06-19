using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Group;
using Domain.Enum.Project;

namespace Application.Services
{
    public class GroupService : IGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notification;
        private readonly IMapper _mapper;

        public GroupService(
            IUnitOfWork unitOfWork,
            INotificationService notification,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _notification = notification;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GroupResponseDTO>> GetAllAsync()
        {
            var groups = await _unitOfWork.Repository<Group>().GetAllAsync();
            var members = await _unitOfWork.Repository<GroupMember>().GetAllAsync();
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return groups.Select(g => Build(g, members, accounts));
        }

        public async Task<GroupResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id);
            if (entity == null) return null;

            var members = await _unitOfWork.Repository<GroupMember>().GetAllAsync();
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return Build(entity, members, accounts);
        }

        public async Task<GroupResponseDTO> CreateAsync(CreateGroupDTO dto)
        {
            var entity = _mapper.Map<Group>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Group>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();

            // Nhóm vừa tạo chưa có member, trả ra DTO rỗng members
            return new GroupResponseDTO
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                OrganizationId = entity.OrganizationId,
                CreatedAt = entity.CreatedAt,
                Members = new List<GroupMemberDTO>()
            };
        }

        public async Task<GroupResponseDTO> UpdateAsync(Guid id, UpdateGroupDTO dto, Guid actor, string? actorRole)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Group with ID {id} not found.", 404);

            await EnsureAdminOrProjectManagerAsync(id, actor, actorRole,
                "Chỉ Admin hoặc PM dự án mới được cập nhật thông tin nhóm.");

            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Group>().Update(entity);
            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(id)
                ?? throw new ApiExceptionResponse("Group not found after update.", 500);
        }

        public async Task DeleteAsync(Guid id, Guid actor, string? actorRole)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Group with ID {id} not found.", 404);

            await EnsureAdminOrProjectManagerAsync(id, actor, actorRole,
                "Chỉ Admin hoặc PM dự án mới được xóa nhóm.");

            _unitOfWork.Repository<Group>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }

        // Đổi vai trò 1 thành viên Active. Role=Leader => chuyển trưởng nhóm (hạ Leader cũ xuống Member).
        public async Task<GroupResponseDTO> ChangeMemberRoleAsync(Guid groupId, Guid accountId, GroupMemberRole newRole, Guid actor, string? actorRole)
        {
            _ = await _unitOfWork.Repository<Group>().GetByIdAsync(groupId)
                ?? throw new ApiExceptionResponse($"Group with ID {groupId} not found.", 404);

            var members = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.GroupId == groupId))
                .ToList();

            var target = members.FirstOrDefault(gm => gm.AccountId == accountId && gm.Status == GroupMemberStatus.Active)
                ?? throw new ApiExceptionResponse("Active member not found in this group.", 404);

            var currentLeader = members.FirstOrDefault(
                gm => gm.Role == GroupMemberRole.Leader && gm.Status == GroupMemberStatus.Active);
            var isAdmin = actorRole == AccountRole.Admin.ToString();
            var isLeader = currentLeader != null && currentLeader.AccountId == actor;
            var isManager = await IsProjectManagerOfGroupAsync(groupId, actor);
            if (!isAdmin && !isLeader && !isManager)
                throw new ApiExceptionResponse(
                    "Chỉ Admin, PM dự án hoặc Trưởng nhóm hiện tại mới được đổi vai trò thành viên.", 403);

            if (newRole == GroupMemberRole.Leader)
            {
                // Chuyển trưởng nhóm: hạ Leader hiện tại xuống Member rồi nâng target lên Leader.
                if (target.Role != GroupMemberRole.Leader)
                {
                    if (currentLeader != null && currentLeader.AccountId != target.AccountId)
                        currentLeader.Role = GroupMemberRole.Member;
                    target.Role = GroupMemberRole.Leader;
                }
            }
            else
            {
                target.Role = GroupMemberRole.Member;
            }

            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(groupId)
                ?? throw new ApiExceptionResponse("Group not found after update.", 500);
        }

        public async Task<GroupResponseDTO> ChangeMemberStatusAsync(
            Guid groupId, Guid accountId, GroupMemberStatus newStatus, Guid actor, string? actorRole, string? actorName)
        {
            var group = await _unitOfWork.Repository<Group>().GetByIdAsync(groupId)
                ?? throw new ApiExceptionResponse($"Group with ID {groupId} not found.", 404);

            await EnsureAdminOrProjectManagerAsync(groupId, actor, actorRole,
                "Chỉ Admin hoặc PM dự án mới được cập nhật trạng thái thành viên.");

            var target = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.GroupId == groupId && gm.AccountId == accountId))
                .FirstOrDefault()
                ?? throw new ApiExceptionResponse("Member not found in this group.", 404);

            if (target.Status == newStatus)
                throw new ApiExceptionResponse("Member already in the requested status.", 409);

            var removed = newStatus == GroupMemberStatus.Left;
            target.Status = newStatus;
            await _unitOfWork.CommitAsync();

            if (removed)
            {
                var senderName = actorName ?? "Quản trị viên";
                await _notification.NotifyAsync(
                    accountId,
                    $"{senderName} đã đưa bạn ra khỏi nhóm \"{group.Name}\".",
                    senderName: senderName,
                    linkType: "Group",
                    linkId: groupId.ToString());
            }

            return await GetByIdAsync(groupId)
                ?? throw new ApiExceptionResponse("Group not found after update.", 500);
        }

        private async Task EnsureAdminOrProjectManagerAsync(Guid groupId, Guid actor, string? actorRole, string message)
        {
            if (actorRole == AccountRole.Admin.ToString()) return;
            if (await IsProjectManagerOfGroupAsync(groupId, actor)) return;
            throw new ApiExceptionResponse(message, 403);
        }

        private async Task<bool> IsProjectManagerOfGroupAsync(Guid groupId, Guid actor)
        {
            var projectIds = (await _unitOfWork.Repository<ProjectParticipant>()
                    .FindAsync(pp => pp.GroupId == groupId && pp.Status == ProjectParticipantStatus.Active))
                .Select(pp => pp.ProjectId)
                .ToHashSet();
            if (projectIds.Count == 0) return false;

            return (await _unitOfWork.Repository<Project>()
                    .FindAsync(p => projectIds.Contains(p.Id) && p.ManagerAccountId == actor))
                .Any();
        }

        // Build DTO + join members + accounts (tra dictionary trong-mem, dataset CDE nhỏ -> chấp nhận).
        private static GroupResponseDTO Build(
            Group group,
            IEnumerable<GroupMember> allMembers,
            IDictionary<Guid, Account> accountIndex)
            => new()
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                OrganizationId = group.OrganizationId,
                CreatedAt = group.CreatedAt,
                Members = allMembers
                    .Where(m => m.GroupId == group.Id && m.Status != GroupMemberStatus.Left)
                    .Select(m => new GroupMemberDTO
                    {
                        AccountId = m.AccountId,
                        UserName = accountIndex.TryGetValue(m.AccountId, out var a) ? a.UserName : "",
                        Email = accountIndex.TryGetValue(m.AccountId, out var ae) ? ae.Email : null,
                        Role = m.Role,
                        Status = m.Status,
                        JoinedAt = m.JoinedAt
                    })
                    .ToList()
            };
    }
}
