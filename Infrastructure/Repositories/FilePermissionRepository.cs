using Application.DTOs.ResponseDTOs.Permission;
using Application.Interfaces.IRepositories;
using Domain.Entities;
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

        //Can tach ra giua get de thay doi voi get de view
        //Ham nay ko co AsnoTracking
        //public async Task<IEnumerable<GroupFilePermissionResponseDTO>> GetPartipatedGroupFilePermissionsByFileItemIdAsync(Guid fileItemId)
        //{
        //    return await _context.FilePermissions
        //        .Where(p => p.FileItemId == fileItemId)
        //        .Select(p => new GroupFilePermissionResponseDTO
        //        {
        //            ProjectParticipantId = p.ProjectParticipant!.GroupId,
        //            GroupParticipantName = p.ProjectParticipant.Group.Name,

        //            CanView = p.CanView,
        //            CanEdit = p.CanEdit,
        //            CanUpdate = p.CanUpdate,
        //            CanDownload = p.CanDownload,
        //            CanVerify = p.CanVerify,
        //            CanApprove = p.CanApprove,
        //            InheritFromParent = p.InheritFromParent
        //        })
        //        .ToListAsync();
        //}

        public async Task<IEnumerable<FilePermission>> GetPartipatedGroupFilePermissionsByFileItemIdAsync(Guid fileItemId)
        {
            return await _context.FilePermissions
                .Where(p => p.FileItemId == fileItemId)
                .Include(p => p.ProjectParticipant)
                .ThenInclude(pp => pp.Group)
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
    }
}
