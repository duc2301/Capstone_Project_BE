using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
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
            var result = await _projectFlow.AssignManagerAsync(id, dto, User.GetUserName());
            return Ok(ApiResponse.Success("Manager assigned", result));
        }

        [HttpPost("{id:guid}/participants/bulk")]
        [Authorize]
        public async Task<IActionResult> AddParticipants(Guid id, [FromBody] AddParticipantsBulkDTO dto)
        {
            var result = await _projectFlow.AddParticipantsAsync(id, dto, User.GetAccountId(), User.GetSystemRole());
            return Ok(ApiResponse.Success($"{result.Count} participant(s) added", result));
        }

        [HttpGet("{id:guid}/participants")]
        [Authorize]
        public async Task<IActionResult> GetParticipants(Guid id)
        {
            var result = await _projectFlow.GetParticipantsAsync(id);
            return Ok(ApiResponse.Success("Participants retrieved", result));
        }

        [HttpPut("{id:guid}/participants/{groupId:guid}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateParticipantStatus(
            Guid id, Guid groupId, [FromBody] UpdateParticipantStatusDTO dto)
        {
            var result = await _projectFlow.UpdateParticipantStatusAsync(id, groupId, dto);
            return Ok(ApiResponse.Success("Participant status updated", result));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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

        [HttpGet("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _projectService.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse("Project not found.", 404);
            return Ok(ApiResponse.Success("Project retrieved", result));
        }

        // Dự án người dùng hiện tại đang tham gia (qua group) hoặc làm PM.
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine()
        {
            var result = await _projectFlow.GetMyProjectsAsync(User.GetAccountId());
            return Ok(ApiResponse.Success("My projects retrieved", result));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDTO dto)
        {
            var result = await _projectService.UpdateAsync(id, dto);
            return Ok(ApiResponse.Success("Project updated", result));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _projectService.DeleteAsync(id);
            return Ok(ApiResponse.Success("Project deleted"));
        }
    }
}
