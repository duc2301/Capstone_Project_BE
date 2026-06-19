using Application.DTOs.RequestDTOs.OrganizationType;
using Application.DTOs.ResponseDTOs.OrganizationType;

namespace Application.Interfaces.IServices
{
    public interface IOrganizationTypeService
    {
        Task<IEnumerable<OrganizationTypeResponseDTO>> GetAllAsync();
        Task<OrganizationTypeResponseDTO?> GetByIdAsync(Guid id);
        Task<OrganizationTypeResponseDTO> CreateAsync(CreateOrganizationTypeDTO dto);
        Task<OrganizationTypeResponseDTO> UpdateAsync(Guid id, UpdateOrganizationTypeDTO dto);
        Task DeleteAsync(Guid id);
    }
}
