using Application.DTOs.RequestDTOs.DigitalSite;
using Application.DTOs.ResponseDTOs.DigitalSite;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/digital-sites")]
    public class DigitalSitesController
        : BaseCrudController<DigitalSite, CreateDigitalSiteDTO, UpdateDigitalSiteDTO, DigitalSiteResponseDTO>
    {
        public DigitalSitesController(
            IGenericService<DigitalSite, CreateDigitalSiteDTO, UpdateDigitalSiteDTO, DigitalSiteResponseDTO> service)
            : base(service) { }
    }
}
