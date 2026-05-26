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

        public ProjectsController(            
            IProjectFlowService projectFlow)
        {
            _projectFlow = projectFlow;
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

        
    }
}
