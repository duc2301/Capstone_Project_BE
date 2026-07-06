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

        // check project exit or not return true or false
        public async Task<bool> ProjectExistsAsync(Guid projectId)
        {
            return await _context.Projects.AnyAsync(p => p.Id == projectId);
        }

        // get folder by id return only the found folder data(sub data not included) or null
        public async Task<Folder?> GetFolderByIdAsync(Guid folderId)
        {
            return await _context.Folders
                .AsNoTracking()
                .SingleOrDefaultAsync(f => f.Id == folderId);
        }

        // get project folders by project id and area return list of folders
        public async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId, CdeArea? area)
        {
            return await _context.Folders
                .Where(f => f.ProjectId == projectId && !f.IsTemplate) // can coi lai cho nay co muc dich gi
                .Where(f => area == null || f.Area == area.Value)
                .AsNoTracking()
                .ToListAsync();
        }

        // get viewable folder ids by project id and account id return hashset of folder ids
        // used for getting set of folder ids that the user has view permission
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


        // check if user is admin or not
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

        public async Task<List<Folder>> GetChildFoldersAsync(Guid parentFolderId)
        {
            return await _context.Folders
                .Where(f => f.ParentFolderId == parentFolderId && !f.IsTemplate)
                .OrderBy(f => f.Name)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
