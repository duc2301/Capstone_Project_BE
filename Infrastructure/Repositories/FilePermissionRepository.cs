using Application.DTOs.ResponseDTOs.Permission;
using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Group;
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
                .Where(p => p.FileItemId == fileItemId
                         && p.Status == PermissionStatus.Active
                         && p.ProjectParticipantId != null)
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
                                GroupName = pp.Group.Name
                            })
                            .AsNoTracking()
                            .ToListAsync();
        }

        /// <summary>
        /// Get the ProjectParticipant ids (within the file's project) that the caller belongs to,
        /// i.e. participants whose group the caller is an active member of.
        /// Used to hide the caller's own group from the permission-assigning UI so they cannot
        /// remove/kick themselves out of the group.
        /// </summary>
        public async Task<HashSet<Guid>> GetCallerParticipantIdsByFileItemIdAsync(Guid fileItemId, Guid accountId)
        {
            var projectId = await _context.FileItems
                            .Where(f => f.Id == fileItemId)
                            .Select(f => f.Folder.ProjectId)
                            .SingleAsync();

            var participantIds = await _context.ProjectParticipants
                            .Where(pp => pp.ProjectId == projectId
                                      && pp.Status == ProjectParticipantStatus.Active
                                      && _context.GroupMembers.Any(gm => gm.GroupId == pp.GroupId
                                                                      && gm.AccountId == accountId
                                                                      && gm.Status == GroupMemberStatus.Active))
                            .Select(pp => pp.Id)
                            .ToListAsync();

            return participantIds.ToHashSet();
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

        // ===== Per-user "Phân quyền thành viên" (blacklist) UI =====

        public async Task<List<GroupGrantDTO>> GetActiveGroupGrantsByFileItemIdAsync(Guid fileItemId)
        {
            // ALL active file group rows (incl. denies), so present-wins can be resolved in the service.
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.Status == PermissionStatus.Active
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active)
                .Select(fp => new GroupGrantDTO
                {
                    ParticipantId = fp.ProjectParticipantId!.Value,
                    GroupName = fp.ProjectParticipant!.Group.Name,
                    CanView = fp.CanView,
                    CanEdit = fp.CanEdit
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<MemberOfParticipantDTO>> GetActiveMembersByParticipantIdsAsync(List<Guid> participantIds)
        {
            if (participantIds.Count == 0) return new List<MemberOfParticipantDTO>();

            return await _context.ProjectParticipants
                .Where(pp => participantIds.Contains(pp.Id))
                .SelectMany(pp => pp.Group.Members
                    .Where(m => m.Status == GroupMemberStatus.Active)
                    .Select(m => new MemberOfParticipantDTO
                    {
                        ParticipantId = pp.Id,
                        AccountId = m.AccountId,
                        UserName = m.Account.UserName,
                        Email = m.Account.Email
                    }))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, FilePermission>> GetActiveAccountOverridesByFileItemIdAsync(Guid fileItemId)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.AccountId != null
                          && fp.Status == PermissionStatus.Active)
                .Include(fp => fp.Account)
                .AsNoTracking()
                .ToDictionaryAsync(fp => fp.AccountId!.Value, fp => fp);
        }

        public async Task<Dictionary<Guid, FilePermission>> GetAccountOverridesByFileItemIdAsync(Guid fileItemId, List<Guid> accountIds)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.AccountId != null
                          && accountIds.Contains(fp.AccountId.Value))
                .ToDictionaryAsync(fp => fp.AccountId!.Value, fp => fp);
        }

        public async Task<List<FilePermission>> GetAccountOverrideRowsByFileItemIdAsync(Guid fileItemId, List<Guid> accountIds)
        {
            return await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.AccountId != null
                          && accountIds.Contains(fp.AccountId.Value))
                .Include(fp => fp.Account)
                .AsNoTracking()
                .ToListAsync();
        }

    }
}
