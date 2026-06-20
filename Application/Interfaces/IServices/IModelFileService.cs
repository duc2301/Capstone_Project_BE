using Application.DTOs.RequestDTOs.ModelFile;
using Application.DTOs.ResponseDTOs.ModelFile;

namespace Application.Interfaces.IServices
{
    public interface IModelFileService
    {
        Task<IEnumerable<ModelFileResponseDTO>> GetAllAsync();
        Task<ModelFileResponseDTO?> GetByIdAsync(Guid id);
        Task<ModelFileResponseDTO> CreateAsync(CreateModelFileDTO dto);
        Task<ModelFileResponseDTO> UpdateAsync(Guid id, UpdateModelFileDTO dto);
        Task DeleteAsync(Guid id);
    }
}
