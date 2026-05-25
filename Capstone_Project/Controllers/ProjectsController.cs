using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/projects")]
    public class ProjectsController
        : BaseCrudController<Project, CreateProjectDTO, UpdateProjectDTO, ProjectResponseDTO>
    {
        private readonly IProjectFlowService _projectFlow;

        public ProjectsController(
            IGenericService<Project, CreateProjectDTO, UpdateProjectDTO, ProjectResponseDTO> service,
            IProjectFlowService projectFlow)
            : base(service)
        {
            _projectFlow = projectFlow;
        }

        // Admin tạo tài khoản Project Manager cho 1 project trống.
        // Atomic: tạo Account + set Project.ManagerAccountId trong cùng transaction.
        [HttpPost("{id:guid}/manager")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateManager(Guid id, [FromBody] CreateProjectManagerDTO dto)
        {
            var result = await _projectFlow.CreateManagerAsync(id, dto);
            return Ok(ApiResponse.Success("Manager created and assigned", result));
        }

        // Project Manager thêm bên tham gia (Organization/Group) vô project.
        [HttpPost("{id:guid}/participants")]
        [Authorize]
        public async Task<IActionResult> AddParticipant(Guid id, [FromBody] AddParticipantDTO dto)
        {
            var result = await _projectFlow.AddParticipantAsync(id, dto);
            return Ok(ApiResponse.Success("Participant added", result));
        }
    }
}
