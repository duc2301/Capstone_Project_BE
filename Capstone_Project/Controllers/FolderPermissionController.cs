using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Permission;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/folder-permissions")]
    public class FolderPermissionController : ControllerBase
    {
        private readonly IFolderPermissionService _folderPermissionService;

        public FolderPermissionController(IFolderPermissionService folderPermissionService)
        {
            _folderPermissionService = folderPermissionService;
        }

        #region folder permission

        // Hàm này dùng để lấy data để test, lấy hết tất cả data liên quan đến file permission, bao gồm cả group đã bị xóa khỏi permission list
        [HttpGet("{folderId:guid}")]
        public async Task<IActionResult> GetParticipatedGroupWithFolderPermissionWithFileItemId(Guid folderId)
        {
            var result = await _folderPermissionService.GetGroupFolderPermissionResponsesAsync(folderId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        
        [HttpGet("{folderId:guid}/group-ui")]
        public async Task<IActionResult> GetDataForFolderPermissionUI(Guid folderId)
        {
            var result = await _folderPermissionService.GetDataForPermissionUIAsync(folderId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        [HttpGet("{folderId:guid}/active-groups")]
        public async Task<IActionResult> GetActiveParticipatedGroupByFolderId(Guid folderId)
        {
            var result = await _folderPermissionService.GetActiveParticipantsByFolderId(folderId);
            return Ok(ApiResponse.Success("Active groups retrieved successfully", result));
        }

        [HttpPost("add-group")]
        public async Task<IActionResult> SaveFolderPermissions([FromBody] AddPermissionsBulkDTO dto)
        {
            //if (dto.FileItemId != fileId)
            //    return BadRequest("File ID mismatch");

            var result = await _folderPermissionService.BulkUpdateFolderPermissionsAsync(dto);
            return Ok(ApiResponse.Success("Permission updated successfully", result));
        }

        [HttpGet("group-permission")]
        public async Task<IActionResult> GetFolderPermissionOfParticipantByFolderIdAndParticipantId([FromQuery] GetFolderPermissionOfParticipantDTO dto)
        {
            var result = await _folderPermissionService.GetFolderPermissionOfParticipantByFolderIdAndParticipantId(dto);
            return Ok(ApiResponse.Success("Group permission retrieved successfully", result));
        }

        #endregion
    }
}
