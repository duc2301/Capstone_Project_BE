using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    public interface IFileItemService
    {
        Task<IEnumerable<FileItemResponseDTO>> GetAllAsync();
        Task<FileItemResponseDTO?> GetByIdAsync(Guid id);
        Task<FileItemResponseDTO> CreateAsync(CreateFileItemDTO dto);
        Task<FileItemResponseDTO> UpdateAsync(Guid id, UpdateFileItemDTO dto);
        Task DeleteAsync(Guid id);

        Task<IEnumerable<FileListItemDTO>> GetByFolderAsync(Guid folderId, Guid actorId);
        Task<IEnumerable<FileVersionResponseDTO>> GetVersionsAsync(Guid fileItemId, Guid actorId);
        Task<TransferZoneResponseDTO> TransferZoneAsync(Guid fileItemId, TransferZoneRequestDTO dto, Guid actorId);
    }
}
