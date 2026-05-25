using Application.DTOs.RequestDTOs.Discussion;
using Application.DTOs.ResponseDTOs.Discussion;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/discussions")]
    public class DiscussionsController
        : BaseCrudController<Discussion, CreateDiscussionDTO, UpdateDiscussionDTO, DiscussionResponseDTO>
    {
        public DiscussionsController(
            IGenericService<Discussion, CreateDiscussionDTO, UpdateDiscussionDTO, DiscussionResponseDTO> service)
            : base(service) { }
    }
}
