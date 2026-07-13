using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Permission;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/file-permissions")]
    public class FilePermissionController : ControllerBase
    {
        private readonly IFilePermissionService _filePermissionService;

        public FilePermissionController(IFilePermissionService filePermissionService)
        {
            _filePermissionService = filePermissionService;
        }

        #region file permission
        // Hàm này dùng để lấy data để test, lấy hết tất cả data liên quan đến file permission, bao gồm cả group đã bị xóa khỏi permission list
        [HttpGet("{fileItemId}")]
        public async Task<IActionResult> GetParticipatedGroupWithFilePermissionWithFileItemId(Guid fileItemId)
        {
            var result = await _filePermissionService.GetGroupFilePermissionResponsesAsync(fileItemId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        //
        [HttpGet("{fileItemId:guid}/group-ui")]
        public async Task<IActionResult> GetDataForFilePermissionUI(Guid fileItemId)
        {
            var result = await _filePermissionService.GetDataForPermissionUIAsync(fileItemId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        [HttpGet("{fileItemId:guid}/active-groups")]
        public async Task<IActionResult> GetActiveParticipatedGroupByFileItemId(Guid fileItemId)
        {
            var result = await _filePermissionService.GetActiveParticipantsByFileItemId(fileItemId);
            return Ok(ApiResponse.Success("Active groups retrieved successfully", result));
        }

        [HttpPost("add-group")]
        public async Task<IActionResult> SaveFilePermissions([FromBody] AddPermissionsBulkDTO dto)
        {
            //if (dto.FileItemId != fileId)
            //    return BadRequest("File ID mismatch");

            var result = await _filePermissionService.BulkUpdateFilePermissionsAsync(dto);
            return Ok(ApiResponse.Success("Permission updated successfully", result));
        }

        [HttpGet("group-permission")]
        public async Task<IActionResult> GetFilePermissionOfParticipantByFileItemIdAndParticipantId([FromQuery] GetFilePermissionOfParticipantDTO dto)
        {
            var result = await _filePermissionService.GetFilePermissionOfParticipantByFileItemIdAndParticipantId(dto);
            return Ok(ApiResponse.Success("Group permission retrieved successfully", result));
        }

        #endregion

        
    }
}
