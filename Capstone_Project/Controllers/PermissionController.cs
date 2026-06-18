using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Permission;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/permissions")]
    public class PermissionController : ControllerBase
    {
        private readonly IFilePermissionService _filePermissionService;

        public PermissionController(IFilePermissionService filePermissionService)
        {
            _filePermissionService = filePermissionService;
        }

        [HttpGet("{fileItemId}")]
        public async Task<IActionResult> GetParticipatedGroupWithFilePermissionWithFileItemId(Guid fileItemId)
        {
            var result = await _filePermissionService.GetGroupFilePermissionResponsesAsync(fileItemId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        [HttpPost("add-group")]
        public async Task<IActionResult> SavePermissions([FromBody] AddPermissionsBulkDTO dto)
        {
            //if (dto.FileItemId != fileId)
            //    return BadRequest("File ID mismatch");

            var result = await _filePermissionService.BulkUpdateFilePermissionsAsync(dto);
            return Ok(ApiResponse.Success("Permission updated successfully", result));
        }
    }
}
