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
    }
}
