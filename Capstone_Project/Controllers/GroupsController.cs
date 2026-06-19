using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Group;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/groups")]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupService _service;

        public GroupsController(IGroupService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetAllAsync()));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateGroupDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupDTO dto)
            => Ok(ApiResponse.Success("Updated successfully",
                await _service.UpdateAsync(id, dto, User.GetAccountId(), User.GetSystemRole())));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id, User.GetAccountId(), User.GetSystemRole());
            return Ok(ApiResponse.Success("Deleted successfully"));
        }

        // Đổi vai trò thành viên (Role=Leader => chuyển trưởng nhóm cho member khác).
        [HttpPut("{groupId:guid}/members/{accountId:guid}/role")]
        public async Task<IActionResult> ChangeMemberRole(Guid groupId, Guid accountId, [FromBody] ChangeMemberRoleDTO dto)
        {
            var result = await _service.ChangeMemberRoleAsync(
                groupId, accountId, dto.Role, User.GetAccountId(), User.GetSystemRole());
            return Ok(ApiResponse.Success("Member role updated", result));
        }

        [HttpPut("{groupId:guid}/members/{accountId:guid}/status")]
        public async Task<IActionResult> ChangeMemberStatus(Guid groupId, Guid accountId, [FromBody] ChangeMemberStatusDTO dto)
        {
            var result = await _service.ChangeMemberStatusAsync(
                groupId, accountId, dto.Status, User.GetAccountId(), User.GetSystemRole(), User.GetUserName());
            return Ok(ApiResponse.Success("Member status updated", result));
        }
    }
}
