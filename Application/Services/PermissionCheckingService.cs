using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Domain.Entities;

namespace Application.Services
{
    /// <summary>
    /// Centralized permission evaluation. Baseline flow only:
    /// look up the user's permission record and check the requested flag —
    /// no inheritance, no PM/Admin bypass, no caching yet.
    /// </summary>
    public class PermissionCheckingService : IPermissionCheckingService
    {
        private readonly IPermissionCheckingRepository _permissionCheckingRepository;

        public PermissionCheckingService(IPermissionCheckingRepository permissionCheckingRepository)
        {
            _permissionCheckingRepository = permissionCheckingRepository;
        }

        // ===== Folder permissions =====

        public Task CanViewFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanView, "View");

        public Task CanEditFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanEdit, "Edit");

        public Task CanUpdateFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanUpdate, "Update");

        public Task CanDownloadFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanDownload, "Download");

        public Task CanVerifyFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanVerify, "Verify");

        public Task CanApproveFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanApprove, "Approve");

        // ===== File permissions =====

        public Task CanViewFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanView, "View");

        public Task CanEditFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanEdit, "Edit");

        public Task CanUpdateFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanUpdate, "Update");

        public Task CanDownloadFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanDownload, "Download");

        public Task CanVerifyFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanVerify, "Verify");

        public Task CanApproveFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanApprove, "Approve");

        // ===== Shared evaluation =====

        private async Task CheckFolderAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission, string action)
        {
            var permission = await _permissionCheckingRepository
                .GetUserFolderPermissionAsync(folderId, accountId);

            if (permission == null || !hasPermission(permission))
                throw new ApiExceptionResponse(
                    $"You do not have '{action}' permission on this folder.", 403);
        }

        private async Task CheckFileAsync(
            Guid fileItemId, Guid accountId, Func<FilePermission, bool> hasPermission, string action)
        {
            var permission = await _permissionCheckingRepository
                .GetUserFilePermissionAsync(fileItemId, accountId);

            if (permission == null || !hasPermission(permission))
                throw new ApiExceptionResponse(
                    $"You do not have '{action}' permission on this file.", 403);
        }
    }
}
