using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Group;
using Domain.Enum.Issue;
using Domain.Enum.Permission;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Retrieval-only repository for the centralized permission checking module.
    /// Resolves accountId -> active GroupMember -> active ProjectParticipant -> permission record.
    /// </summary>
    public class PermissionCheckingRepository : IPermissionCheckingRepository
    {
        private readonly CDESystemDbContext _context;

        public PermissionCheckingRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<FolderPermission?> GetUserFolderPermissionAsync(Guid folderId, Guid accountId)
        {
            return await _context.FolderPermissions
                .Where(fp => fp.FolderId == folderId
                          && fp.Status == PermissionStatus.Active
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                          && fp.ProjectParticipant.Group.Members.Any(m =>
                                 m.AccountId == accountId && m.Status == GroupMemberStatus.Active))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<FilePermission?> GetUserFilePermissionAsync(Guid fileItemId, Guid accountId)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.Status == PermissionStatus.Active
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                          && fp.ProjectParticipant.Group.Members.Any(m =>
                                 m.AccountId == accountId && m.Status == GroupMemberStatus.Active))
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        // ===== Per-account overrides (Google-Drive style user grant/deny) =====

        public async Task<FilePermission?> GetUserFileAccountOverrideAsync(Guid fileItemId, Guid accountId)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.AccountId == accountId
                          && fp.Status == PermissionStatus.Active)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<FolderPermission?> GetNearestFolderAccountOverrideByFileAsync(Guid fileItemId, Guid accountId)
        {
            var info = await _context.FileItems
                .Where(fi => fi.Id == fileItemId)
                .Select(fi => new { fi.FolderId, fi.Folder.ProjectId })
                .FirstOrDefaultAsync();
            if (info == null) return null;

            return await WalkNearestFolderAccountOverrideAsync(info.ProjectId, info.FolderId, accountId);
        }

        public async Task<FolderPermission?> GetNearestFolderAccountOverrideByFolderAsync(Guid folderId, Guid accountId)
        {
            var projectId = await _context.Folders
                .Where(f => f.Id == folderId)
                .Select(f => (Guid?)f.ProjectId)
                .FirstOrDefaultAsync();
            if (projectId == null) return null;

            return await WalkNearestFolderAccountOverrideAsync(projectId.Value, folderId, accountId);
        }

        /// <summary>
        /// Walk from startFolderId up the parent chain (inclusive) and return the first folder that
        /// carries an active per-account override for this user. Fast path: if the user has no folder
        /// override anywhere in the project, skip loading the tree entirely.
        /// </summary>
        private async Task<FolderPermission?> WalkNearestFolderAccountOverrideAsync(
            Guid projectId, Guid startFolderId, Guid accountId)
        {
            var overrides = await _context.FolderPermissions
                .Where(fp => fp.AccountId == accountId
                          && fp.Status == PermissionStatus.Active
                          && fp.Folder.ProjectId == projectId)
                .AsNoTracking()
                .ToListAsync();
            if (overrides.Count == 0) return null;

            var overrideByFolder = overrides.ToDictionary(fp => fp.FolderId);

            var parentByFolder = await _context.Folders
                .Where(f => f.ProjectId == projectId)
                .Select(f => new { f.Id, f.ParentFolderId })
                .ToDictionaryAsync(f => f.Id, f => f.ParentFolderId);

            Guid? current = startFolderId;
            var visited = new HashSet<Guid>();
            while (current.HasValue && visited.Add(current.Value))
            {
                if (overrideByFolder.TryGetValue(current.Value, out var acl))
                    return acl;
                current = parentByFolder.TryGetValue(current.Value, out var parent) ? parent : null;
            }
            return null;
        }

        // ===== Project-admin (PM) full access =====
        // Mirrors FolderTreeRepository.HasFullAccessAsync / GetViewableFolderIdsAsync. Kept as a
        // separate copy so the permission module does not depend on the folder-tree module.

        public async Task<bool> HasProjectAdminAccessAsync(Guid projectId, Guid accountId)
        {
            return await _context.ProjectParticipants
                .AnyAsync(pp => pp.ProjectId == projectId
                             && pp.Status == ProjectParticipantStatus.Active
                             && pp.Role == ProjectParticipantRole.ProjectAdmin
                             && pp.Group.Members.Any(m =>
                                    m.AccountId == accountId && m.Status == GroupMemberStatus.Active));
        }

        public async Task<bool> HasProjectAdminAccessByFolderAsync(Guid folderId, Guid accountId)
        {
            return await _context.ProjectParticipants
                .AnyAsync(pp => pp.Status == ProjectParticipantStatus.Active
                             && pp.Role == ProjectParticipantRole.ProjectAdmin
                             && _context.Folders.Any(f => f.Id == folderId
                                    && f.ProjectId == pp.ProjectId
                                    && f.Area != CdeArea.Wip)
                             && pp.Group.Members.Any(m =>
                                    m.AccountId == accountId && m.Status == GroupMemberStatus.Active));
        }

        public async Task<bool> HasProjectAdminAccessByFileAsync(Guid fileItemId, Guid accountId)
        {
            return await _context.ProjectParticipants
                .AnyAsync(pp => pp.Status == ProjectParticipantStatus.Active
                             && pp.Role == ProjectParticipantRole.ProjectAdmin
                             && _context.FileItems.Any(fi => fi.Id == fileItemId
                                    && fi.Folder.ProjectId == pp.ProjectId
                                    && fi.Folder.Area != CdeArea.Wip)
                             && pp.Group.Members.Any(m =>
                                    m.AccountId == accountId && m.Status == GroupMemberStatus.Active));
        }

        // ===== Project manager (Project.ManagerAccountId) full access =====
        // Scoped to the manager's own project; treated like a system admin (no WIP exclusion).

        public async Task<bool> IsProjectManagerAsync(Guid projectId, Guid accountId)
        {
            return await _context.Projects
                .AnyAsync(p => p.Id == projectId && p.ManagerAccountId == accountId);
        }

        public async Task<bool> IsProjectManagerByFolderAsync(Guid folderId, Guid accountId)
        {
            return await _context.Folders
                .AnyAsync(f => f.Id == folderId && f.Project.ManagerAccountId == accountId);
        }

        public async Task<bool> IsProjectManagerByFileAsync(Guid fileItemId, Guid accountId)
        {
            return await _context.FileItems
                .AnyAsync(fi => fi.Id == fileItemId && fi.Folder.Project.ManagerAccountId == accountId);
        }

        public async Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId)
        {
            var groupViewable = (await _context.FolderPermissions
                .Where(fp => fp.Folder.ProjectId == projectId
                          && fp.Status == PermissionStatus.Active
                          && fp.CanView
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                          && fp.ProjectParticipant.Group.Members.Any(m =>
                                m.AccountId == accountId && m.Status == GroupMemberStatus.Active))
                .Select(fp => fp.FolderId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            // Per-account overrides adjust the group result: an override grants/denies the folder AND
            // its subtree (nearest-ancestor wins), matching the eval-time walk. Fast path: no override
            // in the project -> group result is final, no tree load.
            var overrides = await _context.FolderPermissions
                .Where(fp => fp.AccountId == accountId
                          && fp.Status == PermissionStatus.Active
                          && fp.Folder.ProjectId == projectId)
                .Select(fp => new { fp.FolderId, fp.CanView })
                .ToListAsync();
            if (overrides.Count == 0) return groupViewable;

            var overrideByFolder = overrides.ToDictionary(o => o.FolderId, o => o.CanView);

            var folders = await _context.Folders
                .Where(f => f.ProjectId == projectId)
                .Select(f => new { f.Id, f.ParentFolderId })
                .ToListAsync();
            var parentByFolder = folders.ToDictionary(f => f.Id, f => f.ParentFolderId);

            var result = new HashSet<Guid>();
            foreach (var f in folders)
            {
                // Nearest-ancestor (inclusive) account override decides; else fall back to the group ACL.
                bool? overrideDecision = null;
                Guid? current = f.Id;
                var visited = new HashSet<Guid>();
                while (current.HasValue && visited.Add(current.Value))
                {
                    if (overrideByFolder.TryGetValue(current.Value, out var canView))
                    {
                        overrideDecision = canView;
                        break;
                    }
                    current = parentByFolder.TryGetValue(current.Value, out var parent) ? parent : null;
                }

                if (overrideDecision.HasValue)
                {
                    if (overrideDecision.Value) result.Add(f.Id);
                }
                else if (groupViewable.Contains(f.Id))
                {
                    result.Add(f.Id);
                }
            }
            return result;
        }

        public async Task<bool> HasActiveFileViewGrantAsync(Guid fileItemId, Guid accountId)
        {
            return await _context.FileViewGrants
                .AnyAsync(g => g.FileItemId == fileItemId
                            && g.AccountId == accountId
                            && g.Status == PermissionStatus.Active);
        }

        public async Task<bool> HasIssueStakeholderFileAccessAsync(Guid fileItemId, Guid accountId)
        {
            var area = await _context.FileItems
                .Where(f => f.Id == fileItemId)
                .Select(f => (CdeArea?)f.Folder.Area)
                .FirstOrDefaultAsync();
            if (area != CdeArea.Shared && area != CdeArea.Published) return false;

            return await _context.Issues.AnyAsync(i =>
                i.LinkedFileItemId == fileItemId
                && i.Status != IssueStatus.Closed
                && (i.RaisedByAccountId == accountId
                    || i.AssignedToAccountId == accountId
                    || (i.AssignedToGroupId != null && _context.GroupMembers.Any(m =>
                            m.GroupId == i.AssignedToGroupId
                            && m.AccountId == accountId
                            && m.Status == GroupMemberStatus.Active))
                    || _context.IssueMentions.Any(m =>
                            m.IssueId == i.Id && m.MentionedAccountId == accountId)));
        }

        public async Task<List<Guid>> GetFileIdsWithActivePermissionByFolderAsync(Guid folderId)
        {
            // Cả override nhóm (ProjectParticipantId) lẫn override cá nhân (AccountId) đều là dòng
            // FilePermission -> chỉ những file có dòng Active mới có thể khác quyền so với folder.
            return await _context.FilePermissions
                .Where(fp => fp.FileItem.FolderId == folderId
                          && fp.Status == PermissionStatus.Active)
                .Select(fp => fp.FileItemId)
                .Distinct()
                .ToListAsync();
        }

        // ===== Current-user permission retrieval (viewing only) =====

        public async Task<Account?> GetAccountAsync(Guid accountId)
        {
            return await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId);
        }

        public async Task<Folder?> GetFolderAsync(Guid folderId)
        {
            return await _context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == folderId);
        }

        public async Task<FileItem?> GetFileItemAsync(Guid fileItemId)
        {
            return await _context.FileItems
                .AsNoTracking()
                .FirstOrDefaultAsync(fi => fi.Id == fileItemId);
        }

        public async Task<List<GroupMember>> GetActiveGroupMembershipsAsync(Guid accountId)
        {
            return await _context.GroupMembers
                .Where(m => m.AccountId == accountId && m.Status == GroupMemberStatus.Active)
                .Include(m => m.Group)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<ProjectParticipant>> GetActiveParticipantsByGroupIdsAsync(List<Guid> groupIds)
        {
            return await _context.ProjectParticipants
                .Where(pp => groupIds.Contains(pp.GroupId)
                          && pp.Status == ProjectParticipantStatus.Active)
                .Include(pp => pp.Project)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<FolderPermission>> GetFolderPermissionsByParticipantIdsAsync(List<Guid> participantIds)
        {
            return await _context.FolderPermissions
                .Where(fp => fp.ProjectParticipantId != null
                          && participantIds.Contains(fp.ProjectParticipantId.Value))
                .Include(fp => fp.Folder)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<FilePermission>> GetFilePermissionsByParticipantIdsAsync(List<Guid> participantIds)
        {
            return await _context.FilePermissions
                .Where(fp => fp.ProjectParticipantId != null
                          && participantIds.Contains(fp.ProjectParticipantId.Value))
                .Include(fp => fp.FileItem)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
