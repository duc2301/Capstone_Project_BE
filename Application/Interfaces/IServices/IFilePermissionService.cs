using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFilePermissionService
    {
        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId);

        Task<IEnumerable<GroupFilePermissionResponseDTO>> BulkUpdateFilePermissionsAsync(AddPermissionsBulkDTO dto);

        Task<FilePermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid fileItemId);

        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetActiveParticipantsByFileItemId(Guid fileItemId);

        Task<GroupFilePermissionResponseDTO> GetFilePermissionOfParticipantByFileItemIdAndParticipantId(GetFilePermissionOfParticipantDTO dto);
    }
}