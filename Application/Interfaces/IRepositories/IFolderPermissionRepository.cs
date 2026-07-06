using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IRepositories
{
    public interface IFolderPermissionRepository
    {
        Task<IEnumerable<FolderPermission>> GetPartipatedGroupFolderPermissionsByFolderIdAsync(Guid folderId);
        Task<Dictionary<Guid, FolderPermission>> GetActivePartipantsByFolderIdAsync(Guid folderId);
        Task<IEnumerable<FolderPermission>> GetActiveGroupsByFolderItemId(Guid folderId);
        Task<Dictionary<Guid, FolderPermission>> GetFolderPermissionsByFolderIdAsync(Guid folderId, List<Guid> participantIds);
        Task<IEnumerable<FolderPermission>> GetFolderPermissionsByParticipantIdsAsync(Guid folderId, List<Guid> listFolderPermissionId);
        Task<FolderPermission?> GetFolderPermissionByFolderIdAndParticipantIdAsync(Guid folderId, Guid participantId);
        Task<IEnumerable<ParticipantItems>> GetAllParticipantsByFolderIdAsync(Guid folderId);

    }
}
