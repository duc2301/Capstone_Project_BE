using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Group;
using Domain.Enum.Permission;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FolderTreeRepository : GenericRepository<Folder>, IFolderTreeRepository
    {
        private readonly CDESystemDbContext _context;

        public FolderTreeRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<bool> ProjectExistsAsync(Guid projectId)
        {
            return await _context.Projects.AnyAsync(p => p.Id == projectId);
        }

        public async Task<Folder?> GetFolderByIdAsync(Guid folderId)
        {
            return await _context.Folders
                .AsNoTracking()
                .SingleOrDefaultAsync(f => f.Id == folderId);
        }

        public async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId, CdeArea? area)
        {
            return await _context.Folders
                .Where(f => f.ProjectId == projectId && !f.IsTemplate)
                .Where(f => area == null || f.Area == area.Value)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId)
        {
            var folderIds = await _context.FolderPermissions
                .Where(fp => fp.Folder.ProjectId == projectId
                          && fp.Status == PermissionStatus.Active
                          && fp.CanView
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                          && fp.ProjectParticipant.Group.Members.Any(m =>
                                m.AccountId == accountId && m.Status == GroupMemberStatus.Active))
                .Select(fp => fp.FolderId)
                .Distinct()
                .ToListAsync();

            return folderIds.ToHashSet();
        }

        public async Task<bool> HasFullAccessAsync(Guid projectId, Guid accountId)
        {
            return await _context.ProjectParticipants
                .AnyAsync(pp => pp.ProjectId == projectId
                             && pp.Status == ProjectParticipantStatus.Active
                             && pp.Role == ProjectParticipantRole.ProjectAdmin
                             && pp.Group.Members.Any(m =>
                                    m.AccountId == accountId && m.Status == GroupMemberStatus.Active));
        }

        public async Task<bool> CanViewFolderAsync(Guid folderId, Guid accountId)
        {
            return await _context.FolderPermissions
                .AnyAsync(fp => fp.FolderId == folderId
                             && fp.Status == PermissionStatus.Active
                             && fp.CanView
                             && fp.ProjectParticipant != null
                             && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                             && fp.ProjectParticipant.Group.Members.Any(m =>
                                    m.AccountId == accountId && m.Status == GroupMemberStatus.Active));
        }

        public async Task<List<FileItem>> GetFilesByFolderIdAsync(Guid folderId)
        {
            return await _context.FileItems
                .Where(fi => fi.FolderId == folderId)
                .OrderBy(fi => fi.Name)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
