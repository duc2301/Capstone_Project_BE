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
