using Application.DTOs.ResponseDTOs.Permission;
using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Permission;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Repositories
{
    public class FilePermissionRepository : GenericRepository<FilePermission>, IFilePermissionRepository
    {
        private readonly CDESystemDbContext _context;
        public FilePermissionRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all file permissions for a specific file and include the associated project participant and group information.
        /// This is for getting all the permissions data of a file, no matter if the participant still in the project or not.
        /// For now June 20th, its for testing. Maybe in the future, will it be used for the history permission data.
        /// </summary>
        /// <param name="fileItemId"></param>
        /// <returns>All file permissions for a specific file, including active/inactive permissions</returns>
        public async Task<IEnumerable<FilePermission>> GetPartipatedGroupFilePermissionsByFileItemIdAsync(Guid fileItemId)
        {
            return await _context.FilePermissions
                .Where(p => p.FileItemId == fileItemId)
                .Include(p => p.ProjectParticipant)
                .ThenInclude(pp => pp.Group)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, FilePermission>> GetActivePartipantsByFileItemIdAsync(Guid fileItemId)
        {
            return await _context.FilePermissions
                .Where(p => p.FileItemId == fileItemId && p.Status == PermissionStatus.Active)
                .Include(p => p.ProjectParticipant)
                .ThenInclude(pp => pp.Group)
                .AsNoTracking()
                .ToDictionaryAsync(
                    p => p.ProjectParticipantId!.Value,
                    p => p);
        }

        public async Task<IEnumerable<ParticipantItems>> GetAllParticipantsByFileItemIdAsync(Guid fileItemId)
        {
            var projectId = await _context.FileItems
                            .Where(f => f.Id == fileItemId)
                            .Select(f => f.Folder.ProjectId)
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

        public async Task<IEnumerable<FilePermission>> GetActiveGroupsByFileItemId(Guid fileitemId)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileitemId && fp.Status == PermissionStatus.Active)
                .Include(fp => fp.ProjectParticipant)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, FilePermission>> GetFilePermissionsByFileItemIdAsync(Guid fileItemId, List<Guid> participantIds)
        {
            var existingPermissions = await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && participantIds.Contains(fp.ProjectParticipantId!.Value))
                .ToDictionaryAsync(fp => fp.ProjectParticipantId!.Value);

            return existingPermissions;
        }

        public async Task<IEnumerable<FilePermission>> GetFilePermissionsByParticipantIdsAsync(Guid fileItemId, List<Guid> listFilePermissionId)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && listFilePermissionId.Contains(fp.ProjectParticipantId!.Value))
                .Include(fp => fp.ProjectParticipant)
                .ThenInclude(fp => fp.Group)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<FilePermission?> GetFilePermissionByFileItemIdAndParticipantIdAsync(Guid fileItemId, Guid participantId)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId && fp.ProjectParticipantId == participantId)
                .Include(fp => fp.ProjectParticipant)
                .ThenInclude(fp => fp.Group)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }
    }
}
