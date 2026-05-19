using Application.DTOs.RequestDTOs.Submittal;
using Application.DTOs.ResponseDTOs.Submittal;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/submittals")]
    public class SubmittalsController
        : BaseCrudController<Submittal, CreateSubmittalDTO, UpdateSubmittalDTO, SubmittalResponseDTO>
    {
        public SubmittalsController(
            IGenericService<Submittal, CreateSubmittalDTO, UpdateSubmittalDTO, SubmittalResponseDTO> service)
            : base(service) { }
    }
}
