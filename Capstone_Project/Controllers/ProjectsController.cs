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
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectFlowService _projectFlow;
        private readonly IProjectService _projectService;

        public ProjectsController(IProjectFlowService projectFlow, IProjectService projectService)
        {
            _projectFlow = projectFlow;
            _projectService = projectService;
        }


        // Admin gán 1 account hiện có làm Project Manager.
        // 1 account có thể làm PM nhiều dự án.
        [HttpPost("{id:guid}/manager")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignManager(Guid id, [FromBody] AssignProjectManagerDTO dto)
        {
            var result = await _projectFlow.AssignManagerAsync(id, dto);
            return Ok(ApiResponse.Success("Manager assigned", result));
        }

        // PM add nhiều bên tham gia (department/team/organization) cho project trong 1 transaction.
        // Mỗi item phải đúng 1 trong 3 (DepartmentId / OrganizationId / GroupId).
        [HttpPost("{id:guid}/participants/bulk")]
        [Authorize]
        public async Task<IActionResult> AddParticipants(Guid id, [FromBody] AddParticipantsBulkDTO dto)
        {
            var result = await _projectFlow.AddParticipantsAsync(id, dto);
            return Ok(ApiResponse.Success($"{result.Count} participant(s) added", result));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateProjectDTO dto)
        {
            var result = await _projectService.CreateAsync(dto);
            return Ok(ApiResponse.Success("Project created", result));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var result = await _projectService.GetAllAsync();
            return Ok(ApiResponse.Success("Projects retrieved", result));
        }

        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDTO dto)
        {
            var result = await _projectService.UpdateAsync(id, dto);
            return Ok(ApiResponse.Success("Project updated", result));
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _projectService.DeleteAsync(id);
            return Ok(ApiResponse.Success("Project deleted"));
        }
    }
}
