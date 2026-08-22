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

        // ===== Per-account override UI (Google-Drive style) =====

        public async Task<List<AccountItem>> GetAudienceAccountsByFileItemIdAsync(Guid fileItemId)
        {
            var folderId = await _context.FileItems
                .Where(fi => fi.Id == fileItemId)
                .Select(fi => (Guid?)fi.FolderId)
                .FirstOrDefaultAsync();
            if (folderId == null) return new List<AccountItem>();

            // Groups whose file-level override is PRESENT (grant or deny) — for these the folder ACL
            // is ignored (present-wins), mirroring EvaluateFileAsync.
            var fileOverrideParticipantIds = (await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.Status == PermissionStatus.Active
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active)
                .Select(fp => fp.ProjectParticipantId!.Value)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            // Groups granted VIEW via the file override.
            var fileGrantParticipantIds = await _context.FilePermissions
                .Where(fp => fp.FileItemId == fileItemId
                          && fp.Status == PermissionStatus.Active
                          && fp.CanView
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active)
                .Select(fp => fp.ProjectParticipantId!.Value)
                .Distinct()
                .ToListAsync();

            // Groups granted VIEW via the owning folder (used only where no file override exists).
            var folderGrantParticipantIds = await _context.FolderPermissions
                .Where(fp => fp.FolderId == folderId.Value
                          && fp.Status == PermissionStatus.Active
                          && fp.CanView
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active)
                .Select(fp => fp.ProjectParticipantId!.Value)
                .Distinct()
                .ToListAsync();

            var viewParticipantIds = fileGrantParticipantIds
                .Concat(folderGrantParticipantIds.Where(id => !fileOverrideParticipantIds.Contains(id)))
                .Distinct()
                .ToList();

            return await GetAccountsByParticipantIdsAsync(viewParticipantIds);
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

        private async Task<List<AccountItem>> GetAccountsByParticipantIdsAsync(List<Guid> participantIds)
        {
            if (participantIds.Count == 0) return new List<AccountItem>();

            var accounts = await _context.ProjectParticipants
                .Where(pp => participantIds.Contains(pp.Id))
                .SelectMany(pp => pp.Group.Members
                    .Where(m => m.Status == GroupMemberStatus.Active)
                    .Select(m => m.Account))
                .Select(a => new { a.Id, a.UserName, a.Email })
                .Distinct()
                .AsNoTracking()
                .ToListAsync();

            return accounts
                .Select(a => new AccountItem { AccountId = a.Id, UserName = a.UserName, Email = a.Email })
                .ToList();
        }
    }
}
