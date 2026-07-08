using Application.DTOs.ResponseDTOs.Permission;
using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Permission;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FolderPermissionRepository : GenericRepository<FolderPermission>, IFolderPermissionRepository
    {
        private readonly CDESystemDbContext _context;
        public FolderPermissionRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all folder permissions for a specific folder and include the associated project participant and group information.
        /// This is for getting all the permissions data of a folder, no matter if the participant still in the project or not.
        /// For now June 20th, its for testing. Maybe in the future, will it be used for the history permission data.
        /// </summary>
        /// <param name="folderId"></param>
        /// <returns>All folder permissions for a specific folder</returns>
        public async Task<IEnumerable<FolderPermission>> GetPartipatedGroupFolderPermissionsByFolderIdAsync(Guid folderId)
        {
            return await _context.FolderPermissions
                .Where(p => p.FolderId == folderId)
                .Include(p => p.ProjectParticipant)
                .ThenInclude(pp => pp.Group)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get all active Participants of a folder by their id and include the associated project participant and group information.
        /// This is for the showing the right panel of permission editing table on UI.
        /// </summary>
        /// <param name="folderId"></param>
        /// <returns></returns>
        public async Task<Dictionary<Guid, FolderPermission>> GetActivePartipantsByFolderIdAsync(Guid folderId)
        {
            return await _context.FolderPermissions
                .Where(p => p.FolderId == folderId && p.Status == PermissionStatus.Active)
                .Include(p => p.ProjectParticipant)
                .ThenInclude(pp => pp.Group)
                .AsNoTracking()
                .ToDictionaryAsync(
                    p => p.ProjectParticipantId!.Value,
                    p => p);
        }

        public async Task<IEnumerable<FolderPermission>> GetActiveGroupsByFolderItemId(Guid folderId)
        {
            return await _context.FolderPermissions
                .Where(fp => fp.FolderId == folderId && fp.Status == PermissionStatus.Active)
                .Include(fp => fp.ProjectParticipant)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, FolderPermission>> GetFolderPermissionsByFolderIdAsync(Guid folderId, List<Guid> participantIds)
        {
            var existingPermissions = await _context.FolderPermissions
                .Where(fp => fp.FolderId == folderId
                          && participantIds.Contains(fp.ProjectParticipantId!.Value))
                .ToDictionaryAsync(fp => fp.ProjectParticipantId!.Value);

            return existingPermissions;
        }

        public async Task<IEnumerable<FolderPermission>> GetFolderPermissionsByParticipantIdsAsync(Guid folderId, List<Guid> listFolderPermissionId)
        {
            return await _context.FolderPermissions
                .Where(fp => fp.FolderId == folderId
                          && listFolderPermissionId.Contains(fp.ProjectParticipantId!.Value))
                .Include(fp => fp.ProjectParticipant)
                .ThenInclude(fp => fp.Group)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<FolderPermission?> GetFolderPermissionByFolderIdAndParticipantIdAsync(Guid folderId, Guid participantId)
        {
            return await _context.FolderPermissions
                .Where(fp => fp.FolderId == folderId && fp.ProjectParticipantId == participantId)
                .Include(fp => fp.ProjectParticipant)
                .ThenInclude(fp => fp.Group)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }

        public async Task<IEnumerable<ParticipantItems>> GetAllParticipantsByFolderIdAsync(Guid folderId)
        {
            var projectId = await _context.Folders
                            .Where(f => f.Id == folderId)
                            .Select(f => f.ProjectId)
                            .SingleAsync();

            return await _context.ProjectParticipants
                            .Where(pp => pp.ProjectId == projectId)
                            .Where(pp => pp.Status == ProjectParticipantStatus.Active)
                            .Select(pp => new ParticipantItems
                            {
                                ProjectParticipantId = pp.Id,
                                GroupId = pp.GroupId,
                                GroupName = pp.Group.Name,
                                OrganizationId = pp.Group.OrganizationId,
                                OrganizationName = pp.Group.Organization.DisplayName
                            })
                            .AsNoTracking()
                            .ToListAsync();
        }
    }
}
