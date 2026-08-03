using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;

namespace Application.Interfaces.IServices
{
    public interface IProjectService
    {
        Task<IEnumerable<ProjectResponseDTO>> GetAllAsync();
        Task<ProjectResponseDTO?> GetByIdAsync(Guid id);
        Task<ProjectResponseDTO> CreateAsync(CreateProjectDTO dto, Guid actorId);
        Task<ProjectResponseDTO> UpdateAsync(Guid id, UpdateProjectDTO dto, Guid actorId);
        Task<ProjectResponseDTO> SetImageAsync(
            Guid id, Stream content, string fileName, long sizeBytes, Guid actorId, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid actorId);
    }
}
