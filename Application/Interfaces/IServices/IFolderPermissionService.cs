using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IFolderPermissionService
    {
        Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetGroupFolderPermissionResponsesAsync(Guid folderId);

        Task<FolderPermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid folderId, Guid callerAccountId);

        /// <summary>
        /// Dual-list data for the per-user "Phân quyền" dialog on a folder: users in the folder's
        /// group audience without an override (left) vs users with an active per-account override (right).
        /// </summary>
        Task<UserPermissionsViewModelDTO> GetDataForUserPermissionUIAsync(Guid folderId, Guid callerAccountId);

        Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetActiveParticipantsByFolderId(Guid folderId);

        Task<IEnumerable<GroupFolderPermissionResponseDTO>> BulkUpdateFolderPermissionsAsync(AddPermissionsBulkDTO dto, Guid actorId);

        Task<GroupFolderPermissionResponseDTO> GetFolderPermissionOfParticipantByFolderIdAndParticipantId(GetFolderPermissionOfParticipantDTO dto);
    }
}
