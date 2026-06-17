using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFilePermissionService
        : IGenericService<FilePermission, CreateFilePermissionDTO, UpdateFilePermissionDTO, FilePermissionResponseDTO>
    {
        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId);
    }
}