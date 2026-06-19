using Application.DTOs.RequestDTOs.ProjectModel;
using Application.DTOs.ResponseDTOs.ProjectModel;

namespace Application.Interfaces.IServices
{
    public interface IProjectModelService
    {
        Task<IEnumerable<ProjectModelResponseDTO>> GetAllAsync();
        Task<ProjectModelResponseDTO?> GetByIdAsync(Guid id);
        Task<ProjectModelResponseDTO> CreateAsync(CreateProjectModelDTO dto);
        Task<ProjectModelResponseDTO> UpdateAsync(Guid id, UpdateProjectModelDTO dto);
        Task DeleteAsync(Guid id);
    }
}
