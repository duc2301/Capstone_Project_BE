using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.ResponseDTOs.Organization;
using Application.DTOs.ResponseDTOs.Project;

namespace Application.Interfaces.IServices
{
    public interface IOrganizationService
    {
        Task<IEnumerable<OrganizationResponseDTO>> GetAllAsync();
        Task<OrganizationResponseDTO?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectResponseDTO>> GetProjectsByOrganizationAsync(Guid id);
        Task<OrganizationResponseDTO> CreateAsync(CreateOrganizationDTO dto);
        Task<OrganizationResponseDTO> UpdateAsync(Guid id, UpdateOrganizationDTO dto);
        Task DeleteAsync(Guid id);
    }
}
