using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    public interface IFileItemService
    {
        Task<FileItemResponseDTO?> GetByIdAsync(Guid id);
        // actorId bắt buộc: ba thao tác này đều ghi nhật ký, không có actor thì không truy được ai làm.
        Task<FileItemResponseDTO> CreateAsync(CreateFileItemDTO dto, Guid actorId);
        Task<FileItemResponseDTO> UpdateAsync(Guid id, UpdateFileItemDTO dto, Guid actorId);
        Task DeleteAsync(Guid id, Guid actorId);

        Task<IEnumerable<FileListItemDTO>> GetByFolderAsync(Guid folderId, Guid actorId);
        Task<TransferZoneResponseDTO> TransferZoneAsync(Guid fileItemId, TransferZoneRequestDTO dto, Guid actorId);
    }
}
