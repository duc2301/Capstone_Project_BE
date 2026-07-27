using Application.DTOs.RequestDTOs.Audit;
using Application.DTOs.ResponseDTOs.Audit;
using Application.Interfaces.IRepositories;
using Domain.Enum.Group;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly CDESystemDbContext _context;

        public AuditLogRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<AuditLogPageDTO> QueryAsync(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<Guid>? folderIds,
            HashSet<Guid>? groupIds)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (projectId.HasValue)
                query = query.Where(l => l.ProjectId == projectId.Value);

            // Lọc quyền cho view thành viên: chỉ thấy log của folder mình được xem HOẶC của nhóm mình.
            // Log không thoả (vd: nhóm khác upload vào folder mình không có CanView) sẽ bị ẩn.
            if (folderIds != null || groupIds != null)
            {
                var allowedFolderIds = folderIds ?? new HashSet<Guid>();
                var allowedGroupIds = groupIds ?? new HashSet<Guid>();

                query = query.Where(l =>
                    (l.FolderId != null && allowedFolderIds.Contains(l.FolderId.Value))
                    || (l.GroupId != null && allowedGroupIds.Contains(l.GroupId.Value)));
            }

            if (filter.Scope.HasValue)
                query = query.Where(l => l.Scope == filter.Scope.Value);

            if (filter.Action.HasValue)
                query = query.Where(l => l.Action == filter.Action.Value);

            if (filter.ActorId.HasValue)
                query = query.Where(l => l.ActorAccountId == filter.ActorId.Value);

            if (filter.From.HasValue)
                query = query.Where(l => l.CreatedAt >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(l => l.CreatedAt <= filter.To.Value);

            var total = await query.CountAsync();

            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 || filter.PageSize > 200 ? 20 : filter.PageSize;

            // Join Accounts lấy tên người thao tác (entity lean không snapshot ActorName).
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AuditLogResponseDTO
                {
                    Id = l.Id,
                    Scope = l.Scope,
                    Action = l.Action,
                    ActorAccountId = l.ActorAccountId,
                    ActorName = _context.Accounts
                        .Where(a => a.Id == l.ActorAccountId)
                        .Select(a => a.UserName)
                        .FirstOrDefault(),
                    ProjectId = l.ProjectId,
                    FolderId = l.FolderId,
                    GroupId = l.GroupId,
                    EntityType = l.EntityType,
                    EntityId = l.EntityId,
                    Detail = l.Detail,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

            return new AuditLogPageDTO
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<HashSet<Guid>> GetMyActiveGroupIdsAsync(Guid projectId, Guid accountId)
        {
            var groupIds = await _context.ProjectParticipants
                .Where(pp => pp.ProjectId == projectId
                          && pp.Status == ProjectParticipantStatus.Active
                          && pp.Group.Members.Any(m =>
                                m.AccountId == accountId && m.Status == GroupMemberStatus.Active))
                .Select(pp => pp.GroupId)
                .Distinct()
                .ToListAsync();

            return groupIds.ToHashSet();
        }
    }
}
