using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;

using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Audit;
using Domain.Enum.Group;
using Domain.Enum.Project;

namespace Application.Services
{
    public class GroupService : IGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notification;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLog;
        private readonly IGroupRealtimeNotifier _groupRealtime;
        private readonly IPermissionCleanupService _permissionCleanup;

        public GroupService(
            IUnitOfWork unitOfWork,
            INotificationService notification,
            IMapper mapper,
            IAuditLogService auditLog,
            IGroupRealtimeNotifier groupRealtime,
            IPermissionCleanupService permissionCleanup)
        {
            _unitOfWork = unitOfWork;
            _notification = notification;
            _mapper = mapper;
            _auditLog = auditLog;
            _groupRealtime = groupRealtime;
            _permissionCleanup = permissionCleanup;
        }

        public async Task<IEnumerable<GroupResponseDTO>> GetAllAsync()
        {
            var groups = (await _unitOfWork.Repository<Group>().GetAllAsync()).ToList();
            var groupIds = groups.Select(g => g.Id).ToHashSet();
            var members = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(m => groupIds.Contains(m.GroupId)))
                .ToList();
            var accountIds = members.Select(m => m.AccountId).ToHashSet();
            var accounts = accountIds.Count == 0
                ? new Dictionary<Guid, Account>()
                : (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                    .ToDictionary(a => a.Id);

            return groups.Select(g => Build(g, members, accounts));
        }

        public async Task<GroupResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id);
            if (entity == null) return null;

            var members = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(m => m.GroupId == id))
                .ToList();
            var accountIds = members.Select(m => m.AccountId).ToHashSet();
            var accounts = accountIds.Count == 0
                ? new Dictionary<Guid, Account>()
                : (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                    .ToDictionary(a => a.Id);

            return Build(entity, members, accounts);
        }

        public async Task<GroupResponseDTO> CreateAsync(CreateGroupDTO dto)
        {
            var entity = _mapper.Map<Group>(dto);
            entity.Id = Guid.NewGuid();
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
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
            entity.UpdatedAt = DateTime.UtcNow;
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

            // [T3] Chốt danh sách thành viên Active TRƯỚC khi xóa nhóm — sau commit mới dọn được
            // override mồ côi của từng người (recompute đọc DB đã mất nhóm).
            var memberAccountIds = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.GroupId == id && gm.Status == GroupMemberStatus.Active))
                .Select(gm => gm.AccountId)
                .Distinct()
                .ToList();

            _unitOfWork.Repository<Group>().Delete(entity);
            await _unitOfWork.CommitAsync();

            foreach (var accountId in memberAccountIds)
                await _permissionCleanup.CleanupAccountOverridesAsync(accountId);
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
            await RequireCanChangeMemberRoleAsync(groupId, actor, actorRole, currentLeader);

            var previousRole = target.Role;
            var demotedLeader = ApplyRoleChange(target, newRole, currentLeader);

            // Vai trò thực sự không đổi (vd gọi Leader lên chức Leader) thì bỏ qua ghi log/bắn realtime.
            if (target.Role != previousRole)
                await PersistAndNotifyRoleChangeAsync(groupId, actor, newRole, target, demotedLeader);

            return await GetByIdAsync(groupId)
                ?? throw new ApiExceptionResponse("Group not found after update.", 500);
        }

        private async Task RequireCanChangeMemberRoleAsync(
            Guid groupId, Guid actor, string? actorRole, GroupMember? currentLeader)
        {
            var isAdmin = actorRole == AccountRole.Admin.ToString();
            var isLeader = currentLeader != null && currentLeader.AccountId == actor;
            var isManager = await IsProjectManagerOfGroupAsync(groupId, actor);
            if (!isAdmin && !isLeader && !isManager)
                throw new ApiExceptionResponse(
                    "Chỉ Admin, PM dự án hoặc Trưởng nhóm hiện tại mới được đổi vai trò thành viên.", 403);
        }

        // Mutate target (entity đang tracked) theo newRole. Trả về Leader cũ nếu bị tự động hạ xuống
        // Member do có người khác lên thay (null nếu không có ai bị hạ, kể cả trường hợp no-op).
        private static GroupMember? ApplyRoleChange(GroupMember target, GroupMemberRole newRole, GroupMember? currentLeader)
        {
            if (newRole != GroupMemberRole.Leader)
            {
                target.Role = GroupMemberRole.Member;
                return null;
            }

            if (target.Role == GroupMemberRole.Leader)
                return null;

            GroupMember? demotedLeader = null;
            if (currentLeader != null && currentLeader.AccountId != target.AccountId)
            {
                currentLeader.Role = GroupMemberRole.Member;
                demotedLeader = currentLeader;
            }
            target.Role = GroupMemberRole.Leader;
            return demotedLeader;
        }

        private async Task PersistAndNotifyRoleChangeAsync(
            Guid groupId, Guid actor, GroupMemberRole newRole, GroupMember target, GroupMember? demotedLeader)
        {
            // 1 nhóm có thể thuộc nhiều dự án -> ghi 1 dòng log CHO MỖI dự án để PM thấy đúng nhật ký.
            var projectIds = await GetActiveProjectIdsOfGroupAsync(groupId);

            await LogMemberChangeAsync(AuditAction.Update, target, actor, groupId, projectIds,
                $"Đổi vai trò thành viên thành {(newRole == GroupMemberRole.Leader ? "Trưởng nhóm" : "Thành viên")}");

            if (demotedLeader != null)
                await LogMemberChangeAsync(AuditAction.Update, demotedLeader, actor, groupId, projectIds,
                    "Đổi vai trò thành viên thành Thành viên (do chuyển trưởng nhóm)");

            await _unitOfWork.CommitAsync();

            // Báo realtime cho (các) người vừa bị đổi vai trò để FE tự làm mới ngay.
            await _groupRealtime.MemberRoleChangedAsync(target.AccountId, groupId, target.Role.ToString());
            if (demotedLeader != null)
                await _groupRealtime.MemberRoleChangedAsync(demotedLeader.AccountId, groupId, demotedLeader.Role.ToString());
        }

        // Không có dự án nào (nhóm chưa gắn vào project) thì vẫn ghi 1 dòng chung, không projectId.
        private async Task LogMemberChangeAsync(
            AuditAction action, GroupMember member, Guid actor, Guid groupId,
            IReadOnlyCollection<Guid> projectIds, string detail)
        {
            if (projectIds.Count == 0)
            {
                await _auditLog.LogAsync(
                    LogScope.System, action, nameof(GroupMember), member.Id.ToString(), actor,
                    detail: detail, groupId: groupId);
                return;
            }

            foreach (var projectId in projectIds)
                await _auditLog.LogAsync(
                    LogScope.System, action, nameof(GroupMember), member.Id.ToString(), actor,
                    detail: detail, projectId: projectId, groupId: groupId);
        }

        private async Task<List<Guid>> GetActiveProjectIdsOfGroupAsync(Guid groupId)
            => (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    pp => pp.GroupId == groupId && pp.Status == ProjectParticipantStatus.Active))
                .Select(pp => pp.ProjectId)
                .Distinct()
                .ToList();

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

            var projectIds = await GetActiveProjectIdsOfGroupAsync(groupId);
            await LogMemberChangeAsync(AuditAction.StatusChange, target, actor, groupId, projectIds,
                $"Đổi trạng thái thành viên nhóm '{group.Name}' thành {newStatus}");

            await _unitOfWork.CommitAsync();

            // [T3] Rời/mất Active khỏi nhóm -> tài khoản có thể rớt khỏi pool của các tài nguyên nhóm
            // này cấp View -> dọn override mồ côi (SAU commit; quay lại Active thì không cần dọn).
            if (newStatus != GroupMemberStatus.Active)
                await _permissionCleanup.CleanupAccountOverridesAsync(accountId);

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
