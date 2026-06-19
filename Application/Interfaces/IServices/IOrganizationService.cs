using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.ResponseDTOs.Organization;

namespace Application.Interfaces.IServices
{
    public interface IOrganizationService
    {
        Task<IEnumerable<OrganizationResponseDTO>> GetAllAsync();
        Task<OrganizationResponseDTO?> GetByIdAsync(Guid id);
        Task<OrganizationResponseDTO> CreateAsync(CreateOrganizationDTO dto);
        Task<OrganizationResponseDTO> UpdateAsync(Guid id, UpdateOrganizationDTO dto);
        Task DeleteAsync(Guid id);
    }
}
