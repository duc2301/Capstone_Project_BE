using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Permission;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Domain.Enum.Cde;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/permissions")]
    public class PermissionController : ControllerBase
    {
        private readonly IFilePermissionService _filePermissionService;
        private readonly IFolderPermissionService _folderPermissionService;
        private readonly IFolderTreeService _folderTreeService;

        public PermissionController(IFilePermissionService filePermissionService, IFolderPermissionService folderPermissionService, IFolderTreeService folderTreeService)
        {
            _filePermissionService = filePermissionService;
            _folderPermissionService = folderPermissionService;
            _folderTreeService = folderTreeService;
        }

        #region file permission
        // Hàm này dùng để lấy data để test, lấy hết tất cả data liên quan đến file permission, bao gồm cả group đã bị xóa khỏi permission list
        [HttpGet("/files/{fileItemId}")]
        public async Task<IActionResult> GetParticipatedGroupWithFilePermissionWithFileItemId(Guid fileItemId)
        {
            var result = await _filePermissionService.GetGroupFilePermissionResponsesAsync(fileItemId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        //
        [HttpGet("/files/{fileItemId:guid}/group-ui")]
        public async Task<IActionResult> GetDataForFilePermissionUI(Guid fileItemId)
        {
            var result = await _filePermissionService.GetDataForPermissionUIAsync(fileItemId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        [HttpGet("/files/{fileItemId:guid}/active-groups")]
        public async Task<IActionResult> GetActiveParticipatedGroupByFileItemId(Guid fileItemId)
        {
            var result = await _filePermissionService.GetActiveParticipantsByFileItemId(fileItemId);
            return Ok(ApiResponse.Success("Active groups retrieved successfully", result));
        }

        [HttpPost("/files/add-group")]
        public async Task<IActionResult> SaveFilePermissions([FromBody] AddPermissionsBulkDTO dto)
        {
            //if (dto.FileItemId != fileId)
            //    return BadRequest("File ID mismatch");

            var result = await _filePermissionService.BulkUpdateFilePermissionsAsync(dto);
            return Ok(ApiResponse.Success("Permission updated successfully", result));
        }

        [HttpGet("/files/group-permission")]
        public async Task<IActionResult> GetFilePermissionOfParticipantByFileItemIdAndParticipantId([FromQuery] GetFilePermissionOfParticipantDTO dto)
        {
            var result = await _filePermissionService.GetFilePermissionOfParticipantByFileItemIdAndParticipantId(dto);
            return Ok(ApiResponse.Success("Group permission retrieved successfully", result));
        }

        #endregion

        #region folder permission

        // Hàm này dùng để lấy data để test, lấy hết tất cả data liên quan đến file permission, bao gồm cả group đã bị xóa khỏi permission list
        [HttpGet("/folders/{folderId:guid}")]
        public async Task<IActionResult> GetParticipatedGroupWithFolderPermissionWithFileItemId(Guid folderId)
        {
            var result = await _folderPermissionService.GetGroupFolderPermissionResponsesAsync(folderId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        //
        [HttpGet("/folders/{folderId:guid}/group-ui")]
        public async Task<IActionResult> GetDataForFolderPermissionUI(Guid folderId)
        {
            var result = await _folderPermissionService.GetDataForPermissionUIAsync(folderId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        [HttpGet("/folders/{folderId:guid}/active-groups")]
        public async Task<IActionResult> GetActiveParticipatedGroupByFolderId(Guid folderId)
        {
            var result = await _folderPermissionService.GetActiveParticipantsByFolderId(folderId);
            return Ok(ApiResponse.Success("Active groups retrieved successfully", result));
        }

        [HttpPost("/folders/add-group")]
        public async Task<IActionResult> SaveFolderPermissions([FromBody] AddPermissionsBulkDTO dto)
        {
            //if (dto.FileItemId != fileId)
            //    return BadRequest("File ID mismatch");

            var result = await _folderPermissionService.BulkUpdateFolderPermissionsAsync(dto);
            return Ok(ApiResponse.Success("Permission updated successfully", result));
        }

        [HttpGet("/folders/group-permission")]
        public async Task<IActionResult> GetFolderPermissionOfParticipantByFolderIdAndParticipantId([FromQuery] GetFolderPermissionOfParticipantDTO dto)
        {
            var result = await _folderPermissionService.GetFolderPermissionOfParticipantByFolderIdAndParticipantId(dto);
            return Ok(ApiResponse.Success("Group permission retrieved successfully", result));
        }

        #endregion

        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] Guid projectId, [FromQuery] CdeArea? area)
        {
            var tree = await _folderTreeService.GetTreeAsync(projectId, area);
            return Ok(ApiResponse.Success("CDE tree retrieved", tree));
        }
    }
}
