using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFilePermissionService
    {
        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId);

        Task<IEnumerable<GroupFilePermissionResponseDTO>> BulkUpdateFilePermissionsAsync(AddPermissionsBulkDTO dto, Guid actorId);

        Task<FilePermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid fileItemId, Guid callerAccountId);

        /// <summary>
        /// Dual-list data for the per-user "Phân quyền" dialog on a file: users in the file's group
        /// audience without an override (left) vs users with an active per-account override (right).
        /// </summary>
        Task<UserPermissionsViewModelDTO> GetDataForUserPermissionUIAsync(Guid fileItemId, Guid callerAccountId);

        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetActiveParticipantsByFileItemId(Guid fileItemId);

        Task<GroupFilePermissionResponseDTO> GetFilePermissionOfParticipantByFileItemIdAndParticipantId(GetFilePermissionOfParticipantDTO dto);
    }
}