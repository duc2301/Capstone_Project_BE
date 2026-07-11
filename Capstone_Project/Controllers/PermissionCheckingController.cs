using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    /// <summary>
    /// Endpoint test cho module kiểm tra quyền tập trung.
    /// Trả 200 nếu user hiện tại có quyền, 403 nếu không.
    /// Truyền ?accountId= để kiểm tra quyền của user khác (phục vụ test).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/permission-checking")]
    public class PermissionCheckingController : ControllerBase
    {
        private readonly IPermissionCheckingService _permissionCheckingService;

        public PermissionCheckingController(IPermissionCheckingService permissionCheckingService)
        {
            _permissionCheckingService = permissionCheckingService;
        }

        //private Guid ResolveAccountId(Guid? accountId) => accountId ?? User.GetAccountId();

        #region Current user permissions (viewing only — user always resolved from JWT)

        /// <summary>
        /// Mọi quyền folder/file của user hiện tại: user -> groups -> participants -> permissions.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyPermissions()
        {
            var result = await _permissionCheckingService.GetCurrentUserPermissionsAsync(User.GetAccountId());
            return Ok(ApiResponse.Success("Current user permissions retrieved successfully", result));
        }

        /// <summary>Quyền của user hiện tại trên một folder cụ thể.</summary>
        [HttpGet("me/folders/{folderId:guid}")]
        public async Task<IActionResult> GetMyFolderPermission(Guid folderId)
        {
            var result = await _permissionCheckingService.GetCurrentUserFolderPermissionAsync(folderId, User.GetAccountId());
            return Ok(ApiResponse.Success("Current user folder permission retrieved successfully", result));
        }

        /// <summary>Quyền của user hiện tại trên một file cụ thể.</summary>
        [HttpGet("me/files/{fileItemId:guid}")]
        public async Task<IActionResult> GetMyFilePermission(Guid fileItemId)
        {
            var result = await _permissionCheckingService.GetCurrentUserFilePermissionAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("Current user file permission retrieved successfully", result));
        }

        #endregion

        #region Folder permissions

        [HttpGet("folders/{folderId:guid}/can-view")]
        public async Task<IActionResult> CanViewFolder(Guid folderId)
        {
            await _permissionCheckingService.CanViewFolderAsync(folderId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'View' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-edit")]
        public async Task<IActionResult> CanEditFolder(Guid folderId)
        {
            await _permissionCheckingService.CanEditFolderAsync(folderId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Edit' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-update")]
        public async Task<IActionResult> CanUpdateFolder(Guid folderId)
        {
            await _permissionCheckingService.CanUpdateFolderAsync(folderId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Update' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-download")]
        public async Task<IActionResult> CanDownloadFolder(Guid folderId)
        {
            await _permissionCheckingService.CanDownloadFolderAsync(folderId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Download' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-verify")]
        public async Task<IActionResult> CanVerifyFolder(Guid folderId)
        {
            await _permissionCheckingService.CanVerifyFolderAsync(folderId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Verify' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-approve")]
        public async Task<IActionResult> CanApproveFolder(Guid folderId)
        {
            await _permissionCheckingService.CanApproveFolderAsync(folderId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Approve' permission on this folder."));
        }

        #endregion

        #region File permissions

        [HttpGet("files/{fileItemId:guid}/can-view")]
        public async Task<IActionResult> CanViewFile(Guid fileItemId)
        {
            await _permissionCheckingService.CanViewFileAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'View' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-edit")]
        public async Task<IActionResult> CanEditFile(Guid fileItemId)
        {
            await _permissionCheckingService.CanEditFileAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Edit' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-update")]
        public async Task<IActionResult> CanUpdateFile(Guid fileItemId)
        {
            await _permissionCheckingService.CanUpdateFileAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Update' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-download")]
        public async Task<IActionResult> CanDownloadFile(Guid fileItemId)
        {
            await _permissionCheckingService.CanDownloadFileAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Download' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-verify")]
        public async Task<IActionResult> CanVerifyFile(Guid fileItemId)
        {
            await _permissionCheckingService.CanVerifyFileAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Verify' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-approve")]
        public async Task<IActionResult> CanApproveFile(Guid fileItemId)
        {
            await _permissionCheckingService.CanApproveFileAsync(fileItemId, User.GetAccountId());
            return Ok(ApiResponse.Success("You have 'Approve' permission on this file."));
        }

        #endregion
    }
}
