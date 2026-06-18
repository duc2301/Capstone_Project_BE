using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFileItemService
        : IGenericService<FileItem, CreateFileItemDTO, UpdateFileItemDTO, FileItemResponseDTO>
    {
        // Danh sách file trong 1 folder (gồm version hiện hành + tác giả).
        Task<IEnumerable<FileListItemDTO>> GetByFolderAsync(Guid folderId);

        // Tất cả phiên bản của 1 file (mới nhất trước).
        Task<IEnumerable<FileVersionResponseDTO>> GetVersionsAsync(Guid fileItemId);
    }
}
