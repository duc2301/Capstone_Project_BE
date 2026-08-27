using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Group;
using Domain.Enum.Permission;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Batch data access for the permission matrix. Owns its own queries (no dependency on the
    /// folder-tree module), mirroring PermissionCheckingRepository. Shares the scoped
    /// CDESystemDbContext with UnitOfWork, so entities returned by the "ForUpdate" methods are
    /// tracked and get persisted by IUnitOfWork.CommitAsync.
    /// </summary>
    public class PermissionMatrixRepository : IPermissionMatrixRepository
    {
        private readonly CDESystemDbContext _context;

        public PermissionMatrixRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public Task<bool> ProjectExistsAsync(Guid projectId)
            => _context.Projects.AnyAsync(p => p.Id == projectId);

        // ===== Access gate (leader) =====

        public Task<bool> IsLeaderInProjectAsync(Guid projectId, Guid accountId)
            => _context.GroupMembers.AnyAsync(m =>
                    m.AccountId == accountId
                    && m.Status == GroupMemberStatus.Active
                    && m.Role == GroupMemberRole.Leader
                    && _context.ProjectParticipants.Any(pp =>
                           pp.GroupId == m.GroupId
                           && pp.ProjectId == projectId
                           && pp.Status == ProjectParticipantStatus.Active));

        public Task<bool> IsProjectManagerAsync(Guid projectId, Guid accountId)
            => _context.Projects.AnyAsync(p => p.Id == projectId && p.ManagerAccountId == accountId);

        // ===== Columns =====

        public async Task<List<ProjectParticipant>> GetActiveParticipantsByProjectAsync(Guid projectId)
            => await _context.ProjectParticipants
                .Where(pp => pp.ProjectId == projectId && pp.Status == ProjectParticipantStatus.Active)
                .Include(pp => pp.Group)
                .AsNoTracking()
                .ToListAsync();

        public async Task<HashSet<Guid>> GetCallerParticipantIdsAsync(Guid projectId, Guid accountId)
        {
            var ids = await _context.ProjectParticipants
                .Where(pp => pp.ProjectId == projectId
                          && pp.Status == ProjectParticipantStatus.Active
                          && _context.GroupMembers.Any(gm => gm.GroupId == pp.GroupId
                                                          && gm.AccountId == accountId
                                                          && gm.Status == GroupMemberStatus.Active))
                .Select(pp => pp.Id)
                .ToListAsync();
            return ids.ToHashSet();
        }

        // ===== Row data =====

        public async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId)
            => await _context.Folders
                .Where(f => f.ProjectId == projectId && !f.IsTemplate)
                .AsNoTracking()
                .ToListAsync();

        public async Task<List<FileItem>> GetFilesByFolderIdsAsync(IReadOnlyCollection<Guid> folderIds)
        {
            if (folderIds.Count == 0) return new List<FileItem>();
            return await _context.FileItems
                .Where(fi => folderIds.Contains(fi.FolderId))
                .OrderBy(fi => fi.Name)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<FileItem>> GetFilesByIdsAsync(IReadOnlyCollection<Guid> fileIds)
        {
            if (fileIds.Count == 0) return new List<FileItem>();
            return await _context.FileItems
                .Where(fi => fileIds.Contains(fi.Id))
                .AsNoTracking()
                .ToListAsync();
        }

        // ===== Current cell values (active only) =====

        public async Task<List<FolderPermission>> GetActiveFolderPermissionsByFolderIdsAsync(IReadOnlyCollection<Guid> folderIds)
        {
            if (folderIds.Count == 0) return new List<FolderPermission>();
            return await _context.FolderPermissions
                .Where(fp => folderIds.Contains(fp.FolderId)
                          && fp.Status == PermissionStatus.Active
                          && fp.ProjectParticipantId != null)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<FilePermission>> GetActiveFilePermissionsByFileIdsAsync(IReadOnlyCollection<Guid> fileIds)
        {
            if (fileIds.Count == 0) return new List<FilePermission>();
            return await _context.FilePermissions
                .Where(fp => fileIds.Contains(fp.FileItemId)
                          && fp.Status == PermissionStatus.Active
                          && fp.ProjectParticipantId != null)
                .AsNoTracking()
                .ToListAsync();
        }

        // ===== Save: tracked loads for update =====

        public async Task<List<FolderPermission>> GetFolderPermissionsForUpdateAsync(
            IReadOnlyCollection<Guid> folderIds, IReadOnlyCollection<Guid> participantIds)
        {
            if (folderIds.Count == 0 || participantIds.Count == 0) return new List<FolderPermission>();
            return await _context.FolderPermissions
                .Where(fp => folderIds.Contains(fp.FolderId)
                          && fp.ProjectParticipantId != null
                          && participantIds.Contains(fp.ProjectParticipantId.Value))
                .ToListAsync();
        }

        public async Task<List<FilePermission>> GetFilePermissionsForUpdateAsync(
            IReadOnlyCollection<Guid> fileIds, IReadOnlyCollection<Guid> participantIds)
        {
            if (fileIds.Count == 0 || participantIds.Count == 0) return new List<FilePermission>();
            return await _context.FilePermissions
                .Where(fp => fileIds.Contains(fp.FileItemId)
                          && fp.ProjectParticipantId != null
                          && participantIds.Contains(fp.ProjectParticipantId.Value))
                .ToListAsync();
        }
    }
}
