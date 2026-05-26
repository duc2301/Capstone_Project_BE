using Application.DTOs.RequestDTOs.OrganizationType;
using Application.DTOs.ResponseDTOs.OrganizationType;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IOrganizationTypeService
        : IGenericService<OrganizationType, CreateOrganizationTypeDTO, UpdateOrganizationTypeDTO, OrganizationTypeResponseDTO>
    {
    }
}
