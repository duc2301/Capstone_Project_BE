using Application.DTOs.RequestDTOs.OrganizationType;
using Application.DTOs.ResponseDTOs.OrganizationType;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/organization-types")]
    public class OrganizationTypesController
        : BaseCrudController<OrganizationType, CreateOrganizationTypeDTO, UpdateOrganizationTypeDTO, OrganizationTypeResponseDTO>
    {
        public OrganizationTypesController(
            IGenericService<OrganizationType, CreateOrganizationTypeDTO, UpdateOrganizationTypeDTO, OrganizationTypeResponseDTO> service)
            : base(service) { }
    }
}
