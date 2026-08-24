using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Domain.Enum.Project;

namespace Application.Interfaces.IServices
{
    public interface IProjectService
    {
        Task<PagedResult<ProjectResponseDTO>> GetAllAsync(
            int page,
            int pageSize,
            string? search = null,
            ProjectStatus? status = null,
            Guid? ownerOrganizationId = null);
        Task<List<ProjectResponseDTO>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);
        Task<PagedResult<ProjectResponseDTO>> GetByIdsPagedAsync(
            IReadOnlyCollection<Guid> ids,
            int page,
            int pageSize,
            string? search = null,
            ProjectStatus? status = null,
            Guid? ownerOrganizationId = null);
        Task<ProjectResponseDTO?> GetByIdAsync(Guid id);
        Task<ProjectResponseDTO> CreateAsync(CreateProjectDTO dto, Guid actorId);
        Task<ProjectResponseDTO> UpdateAsync(Guid id, UpdateProjectDTO dto, Guid actorId, bool isSystemAdmin);
        Task<ProjectResponseDTO> SetImageAsync(
            Guid id, Stream content, string fileName, long sizeBytes, Guid actorId, bool isSystemAdmin,
            CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid actorId);
    }
}
