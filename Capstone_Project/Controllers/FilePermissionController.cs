using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Permission;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/file-permissions")]
    [Authorize]
    public class FilePermissionController : ControllerBase
    {
        private readonly IFilePermissionService _filePermissionService;

        public FilePermissionController(IFilePermissionService filePermissionService)
        {
            _filePermissionService = filePermissionService;
        }

        #region file permission

        // [Part 2 — group file-permissioning RESTORED] These GROUP endpoints back the file "Phân quyền"
        // group dialog. They were commented out under KILL-A (2026-08-25) and are re-enabled here.
        // The PERMISSION MATRIX stays folder-only (PermissionMatrixService still feeds no file rows) —
        // file group assignment happens ONLY through these dedicated endpoints, never the matrix.
        // The per-member roster endpoints ("Phân quyền thành viên": user-ui / add-user) are unchanged.

        // Toàn bộ dòng file permission (kể cả group đã bị gỡ khỏi danh sách) — dùng để lấy data/debug.
        [HttpGet("{fileItemId:guid}")]
        public async Task<IActionResult> GetParticipatedGroupWithFilePermissionWithFileItemId(Guid fileItemId)
        {
            var result = await _filePermissionService.GetGroupFilePermissionResponsesAsync(fileItemId);
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        // Data cho hộp thoại phân quyền NHÓM của file: nhóm khả dụng + nhóm đang được cấp quyền.
        [HttpGet("{fileItemId:guid}/group-ui")]
        public async Task<IActionResult> GetDataForFilePermissionUI(Guid fileItemId)
        {
            var result = await _filePermissionService.GetDataForPermissionUIAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("Group with permission retrieved successfully", result));
        }

        [HttpGet("{fileItemId:guid}/active-groups")]
        public async Task<IActionResult> GetActiveParticipatedGroupByFileItemId(Guid fileItemId)
        {
            var result = await _filePermissionService.GetActiveParticipantsByFileItemId(fileItemId);
            return Ok(ApiResponse.Success("Active groups retrieved successfully", result));
        }

        // "Phân quyền thành viên": roster of members with group-inherited access + blacklist state.
        [HttpGet("{fileItemId:guid}/user-ui")]
        public async Task<IActionResult> GetFileMemberPermissions(Guid fileItemId)
        {
            var result = await _filePermissionService.GetMemberPermissionsAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("Members with permission retrieved successfully", result));
        }

        // Blacklist / un-blacklist members on a file (blacklist = usersPermission with canView=false).
        [HttpPost("add-user")]
        public async Task<IActionResult> SaveFileUserPermissions([FromBody] AddUserPermissionsBulkDTO dto)
        {
            var result = await _filePermissionService.BulkUpdateFileUserPermissionsAsync(dto, User.GetAccountId());
            return Ok(ApiResponse.Success("User permission updated successfully", result));
        }

        // Cấp/gỡ quyền NHÓM trên file (present-wins override; gỡ = trả về kế thừa quyền thư mục).
        [HttpPost("add-group")]
        public async Task<IActionResult> SaveFilePermissions([FromBody] AddPermissionsBulkDTO dto)
        {
            var result = await _filePermissionService.BulkUpdateFilePermissionsAsync(dto, User.GetAccountId());
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
