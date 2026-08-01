using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;

namespace Application.Interfaces.IServices
{
    public interface IProjectService
    {
        Task<IEnumerable<ProjectResponseDTO>> GetAllAsync();
        Task<List<ProjectResponseDTO>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);
        Task<ProjectResponseDTO?> GetByIdAsync(Guid id);
        Task<ProjectResponseDTO> CreateAsync(CreateProjectDTO dto, Guid actorId);
        Task<ProjectResponseDTO> UpdateAsync(Guid id, UpdateProjectDTO dto, Guid actorId, bool isSystemAdmin);
        Task<ProjectResponseDTO> SetImageAsync(
            Guid id, Stream content, string fileName, long sizeBytes, Guid actorId, bool isSystemAdmin,
            CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid actorId);
    }
}
