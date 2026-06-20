using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;

namespace Application.Interfaces.IServices
{
    public interface IFolderService
    {
        Task<IEnumerable<FolderResponseDTO>> GetAllAsync();
        Task<FolderResponseDTO?> GetByIdAsync(Guid id);
        Task<FolderResponseDTO> CreateAsync(CreateFolderDTO dto);
        Task<FolderResponseDTO> UpdateAsync(Guid id, UpdateFolderDTO dto);
        Task DeleteAsync(Guid id);
    }
}
