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

        private Guid ResolveAccountId(Guid? accountId) => accountId ?? User.GetAccountId();

        #region Folder permissions

        [HttpGet("folders/{folderId:guid}/can-view")]
        public async Task<IActionResult> CanViewFolder(Guid folderId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanViewFolderAsync(folderId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'View' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-edit")]
        public async Task<IActionResult> CanEditFolder(Guid folderId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanEditFolderAsync(folderId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Edit' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-update")]
        public async Task<IActionResult> CanUpdateFolder(Guid folderId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanUpdateFolderAsync(folderId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Update' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-download")]
        public async Task<IActionResult> CanDownloadFolder(Guid folderId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanDownloadFolderAsync(folderId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Download' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-verify")]
        public async Task<IActionResult> CanVerifyFolder(Guid folderId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanVerifyFolderAsync(folderId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Verify' permission on this folder."));
        }

        [HttpGet("folders/{folderId:guid}/can-approve")]
        public async Task<IActionResult> CanApproveFolder(Guid folderId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanApproveFolderAsync(folderId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Approve' permission on this folder."));
        }

        #endregion

        #region File permissions

        [HttpGet("files/{fileItemId:guid}/can-view")]
        public async Task<IActionResult> CanViewFile(Guid fileItemId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanViewFileAsync(fileItemId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'View' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-edit")]
        public async Task<IActionResult> CanEditFile(Guid fileItemId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanEditFileAsync(fileItemId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Edit' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-update")]
        public async Task<IActionResult> CanUpdateFile(Guid fileItemId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanUpdateFileAsync(fileItemId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Update' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-download")]
        public async Task<IActionResult> CanDownloadFile(Guid fileItemId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanDownloadFileAsync(fileItemId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Download' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-verify")]
        public async Task<IActionResult> CanVerifyFile(Guid fileItemId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanVerifyFileAsync(fileItemId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Verify' permission on this file."));
        }

        [HttpGet("files/{fileItemId:guid}/can-approve")]
        public async Task<IActionResult> CanApproveFile(Guid fileItemId, [FromQuery] Guid? accountId)
        {
            await _permissionCheckingService.CanApproveFileAsync(fileItemId, ResolveAccountId(accountId));
            return Ok(ApiResponse.Success("You have 'Approve' permission on this file."));
        }

        #endregion
    }
}
