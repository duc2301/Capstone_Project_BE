using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Group;
using Application.Interfaces.IServices;
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
        public async Task<IActionResult> Create([FromBody] CreateGroupDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }

        // Đổi vai trò thành viên (Role=Leader => chuyển trưởng nhóm cho member khác).
        [HttpPut("{groupId:guid}/members/{accountId:guid}/role")]
        public async Task<IActionResult> ChangeMemberRole(Guid groupId, Guid accountId, [FromBody] ChangeMemberRoleDTO dto)
        {
            var result = await _service.ChangeMemberRoleAsync(groupId, accountId, dto.Role);
            return Ok(ApiResponse.Success("Member role updated", result));
        }
    }
}
