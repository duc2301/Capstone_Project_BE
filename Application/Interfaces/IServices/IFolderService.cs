using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;

namespace Application.Interfaces.IServices
{
    public interface IFolderService
    {
        Task<IEnumerable<FolderResponseDTO>> GetAllAsync();
        Task<FolderResponseDTO?> GetByIdAsync(Guid id);
        Task<FolderResponseDTO> CreateAsync(CreateFolderDTO dto, Guid actorId);
        Task<FolderResponseDTO> UpdateAsync(Guid id, UpdateFolderDTO dto, Guid actorId);
        Task DeleteAsync(Guid id, Guid actorId);
    }
}
