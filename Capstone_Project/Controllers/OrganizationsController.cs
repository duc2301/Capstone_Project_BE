using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.ResponseDTOs.Organization;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/organizations")]
    public class OrganizationsController
        : BaseCrudController<Organization, CreateOrganizationDTO, UpdateOrganizationDTO, OrganizationResponseDTO>
    {
        public OrganizationsController(
            IGenericService<Organization, CreateOrganizationDTO, UpdateOrganizationDTO, OrganizationResponseDTO> service)
            : base(service) { }
    }
}
