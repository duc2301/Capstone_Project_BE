using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IRepositories
{
    public interface IFilePermissionRepository
    {
        Task<IEnumerable<FilePermission>> GetPartipatedGroupFilePermissionsByFileItemIdAsync(Guid fileItemId);
        Task<Dictionary<Guid, FilePermission>> GetFilePermissionsByFileItemIdAsync(Guid fileItemId, List<Guid> participantIds);
        Task<IEnumerable<FilePermission>> GetFilePermissionsByParticipantIdsAsync(Guid fileItemId, List<Guid> listFilePermissionId);
    }
}
