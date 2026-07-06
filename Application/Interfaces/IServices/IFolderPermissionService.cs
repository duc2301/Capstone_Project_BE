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

        Task<FolderPermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid folderId);

        Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetActiveParticipantsByFolderId(Guid folderId);

        Task<IEnumerable<GroupFolderPermissionResponseDTO>> BulkUpdateFolderPermissionsAsync(AddPermissionsBulkDTO dto);

        Task<GroupFolderPermissionResponseDTO> GetFolderPermissionOfParticipantByFolderIdAndParticipantId(GetFolderPermissionOfParticipantDTO dto);
    }
}
