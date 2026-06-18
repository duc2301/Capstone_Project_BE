using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFilePermissionService
        : IGenericService<FilePermission, CreateFilePermissionDTO, UpdateFilePermissionDTO, FilePermissionResponseDTO>
    {
        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId);

        Task<IEnumerable<FilePermissionResponseDTO>> BulkUpdateFilePermissionsAsync(AddPermissionsBulkDTO dto);

        Task<FilePermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid fileItemId);

        Task<IEnumerable<FilePermissionResponseDTO>> GetActiveParticipantsByFileItemId(Guid fileItemId);
    }
}