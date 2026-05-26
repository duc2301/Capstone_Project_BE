using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.ResponseDTOs.Organization;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IOrganizationService
        : IGenericService<Organization, CreateOrganizationDTO, UpdateOrganizationDTO, OrganizationResponseDTO>
    {
    }
}
