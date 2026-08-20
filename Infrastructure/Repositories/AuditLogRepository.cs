using Application.DTOs.RequestDTOs.Audit;
using Application.DTOs.ResponseDTOs.Audit;
using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Group;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 200;

        private readonly CDESystemDbContext _context;

        public AuditLogRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        // Ăn index (ActorAccountId, Action, CreatedAt) khai trong CDESystemDbContext.
        public async Task<bool> HasRecentAsync(
            Domain.Enum.Audit.AuditAction action, string entityType, string entityId,
            Guid actorId, DateTime since)
        {
            return await _context.Set<AuditLog>()
                .AsNoTracking()
                .AnyAsync(l => l.ActorAccountId == actorId
                            && l.Action == action
                            && l.EntityType == entityType
                            && l.EntityId == entityId
                            && l.CreatedAt >= since);
        }

        public async Task<AuditLogPageDTO> QueryAsync(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<Guid>? folderIds,
            HashSet<Guid>? groupIds)
        {
            var query = BuildQuery(filter, projectId, folderIds, groupIds);

            var total = await query.CountAsync();

            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 || filter.PageSize > MaxPageSize
                ? DefaultPageSize
                : filter.PageSize;

            var items = await ToResponse(query)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new AuditLogPageDTO
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<List<AuditLogResponseDTO>> QueryAllAsync(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<Guid>? folderIds,
            HashSet<Guid>? groupIds,
            int maxRows)
        {
            var query = BuildQuery(filter, projectId, folderIds, groupIds);
            return await ToResponse(query).Take(maxRows).ToListAsync();
        }

        public async Task<AuditLogPageDTO> QueryByEntitiesAsync(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<string> entityTypes,
            HashSet<string> entityIds)
        {
            var query = BuildQuery(filter, projectId, null, null, entityTypes, entityIds);

            var total = await query.CountAsync();

            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 || filter.PageSize > MaxPageSize
                ? DefaultPageSize
                : filter.PageSize;

            var items = await ToResponse(query)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new AuditLogPageDTO
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private IQueryable<AuditLog> BuildQuery(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<Guid>? folderIds,
            HashSet<Guid>? groupIds,
            HashSet<string>? entityTypes = null,
            HashSet<string>? entityIds = null)
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

            if (!string.IsNullOrWhiteSpace(filter.EntityType))
                query = query.Where(l => l.EntityType == filter.EntityType);

            if (!string.IsNullOrWhiteSpace(filter.EntityId))
                query = query.Where(l => l.EntityId == filter.EntityId);

            if (entityTypes != null && entityIds != null)
                query = query.Where(l =>
                    entityTypes.Contains(l.EntityType) && entityIds.Contains(l.EntityId));

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                query = query.Where(l =>
                    (l.Detail != null && l.Detail.ToLower().Contains(term))
                    || _context.Accounts.Any(a =>
                        a.Id == l.ActorAccountId
                        && (a.UserName.ToLower().Contains(term) || a.Email.ToLower().Contains(term))));
            }

            if (filter.From.HasValue)
            {
                var from = DateTime.SpecifyKind(filter.From.Value, DateTimeKind.Utc);
                query = query.Where(l => l.CreatedAt >= from);
            }

            if (filter.To.HasValue)
            {
                var toExclusive = DateTime.SpecifyKind(filter.To.Value, DateTimeKind.Utc);
                query = query.Where(l => l.CreatedAt < toExclusive);
            }

            return query;
        }

        private IQueryable<AuditLogResponseDTO> ToResponse(IQueryable<AuditLog> query) =>
            query
                .OrderByDescending(l => l.CreatedAt)
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
                });

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
