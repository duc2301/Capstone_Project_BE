using Application.DTOs.ResponseDTOs.PermissionChecking;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Domain.Entities;
using Domain.Enum.Account;

namespace Application.Services
{
    /// <summary>
    /// Centralized permission evaluation. Flow:
    /// system admin bypasses everything; otherwise look up the user's permission record
    /// and check the requested flag. A FilePermission record is an OVERRIDE — when a file
    /// has none (the normal case, since they are only created by explicit admin action),
    /// the check falls back to the owning folder's ACL. No PM bypass, no caching yet.
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

        //public Task CanUpdateFolderAsync(Guid folderId, Guid accountId)
        //    => CheckFolderAsync(folderId, accountId, fp => fp.CanUpdate, "Update");

        public Task CanUploadToFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanEdit, "Edit");

        //public Task CanDownloadFolderAsync(Guid folderId, Guid accountId)
        //    => CheckFolderAsync(folderId, accountId, fp => fp.CanDownload, "Download");

        //public Task CanVerifyFolderAsync(Guid folderId, Guid accountId)
        //    => CheckFolderAsync(folderId, accountId, fp => fp.CanVerify, "Verify");

        public Task CanApproveFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanApprove, "Approve");

        // ===== File permissions =====

        // Mỗi method truyền 2 selector cùng nghĩa: một đọc cờ trên FilePermission (override riêng
        // của file), một đọc cờ tương ứng trên FolderPermission (dùng khi file chưa có override).

        public Task CanViewFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanView, fp => fp.CanView, "View");

        public Task CanEditFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanEdit, fp => fp.CanEdit, "Edit");

        //public Task CanUpdateFileAsync(Guid fileItemId, Guid accountId)
        //    => CheckFileAsync(fileItemId, accountId, fp => fp.CanUpdate, fp => fp.CanUpdate, "Update");

        //public Task CanDownloadFileAsync(Guid fileItemId, Guid accountId)
        //    => CheckFileAsync(fileItemId, accountId, fp => fp.CanDownload, fp => fp.CanDownload, "Download");

        //public Task CanVerifyFileAsync(Guid fileItemId, Guid accountId)
        //    => CheckFileAsync(fileItemId, accountId, fp => fp.CanVerify, fp => fp.CanVerify, "Verify");

        public Task CanApproveFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanApprove, fp => fp.CanApprove, "Approve");

        // ===== Current-user permission retrieval (viewing only) =====

        public async Task<CurrentUserPermissionsResponseDTO> GetCurrentUserPermissionsAsync(Guid accountId)
        {
            var account = await GetAccountOrThrowAsync(accountId);

            // account -> active group memberships -> active participants -> all permission records
            var memberships = await _permissionCheckingRepository.GetActiveGroupMembershipsAsync(accountId);
            var groupIds = memberships.Select(m => m.GroupId).Distinct().ToList();

            var participants = await _permissionCheckingRepository.GetActiveParticipantsByGroupIdsAsync(groupIds);
            var participantIds = participants.Select(pp => pp.Id).ToList();

            var folderPermissions = await _permissionCheckingRepository.GetFolderPermissionsByParticipantIdsAsync(participantIds);
            var filePermissions = await _permissionCheckingRepository.GetFilePermissionsByParticipantIdsAsync(participantIds);

            return new CurrentUserPermissionsResponseDTO
            {
                CurrentUser = BuildCurrentUser(account),
                Groups = memberships
                    .Select(m => m.Group)
                    .DistinctBy(g => g.Id)
                    .Select(g => new CurrentUserGroupDTO
                    {
                        GroupId = g.Id,
                        Name = g.Name
                    })
                    .ToList(),
                ProjectParticipants = participants
                    .Select(pp => new CurrentUserParticipantDTO
                    {
                        ProjectParticipantId = pp.Id,
                        ProjectId = pp.ProjectId,
                        ProjectName = pp.Project.ProjectName,
                        GroupId = pp.GroupId,
                        Role = pp.Role,
                        Status = pp.Status
                    })
                    .ToList(),
                Permissions = new CurrentUserPermissionListDTO
                {
                    FolderPermissions = folderPermissions
                        .Select(fp => BuildFolderPermissionItem(fp, fp.Folder.Name))
                        .ToList(),
                    FilePermissions = filePermissions
                        .Select(fp => BuildFilePermissionItem(fp, fp.FileItem.Name))
                        .ToList()
                }
            };
        }

        public async Task<CurrentUserFolderPermissionResponseDTO> GetCurrentUserFolderPermissionAsync(Guid folderId, Guid accountId)
        {
            var account = await GetAccountOrThrowAsync(accountId);

            var folder = await _permissionCheckingRepository.GetFolderAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            var permission = await _permissionCheckingRepository.GetUserFolderPermissionAsync(folderId, accountId);

            return new CurrentUserFolderPermissionResponseDTO
            {
                CurrentUser = BuildCurrentUser(account),
                FolderId = folder.Id,
                FolderName = folder.Name,
                Permission = permission == null ? null : BuildFolderPermissionItem(permission, folder.Name)
            };
        }

        public async Task<CurrentUserFilePermissionResponseDTO> GetCurrentUserFilePermissionAsync(Guid fileItemId, Guid accountId)
        {
            var account = await GetAccountOrThrowAsync(accountId);

            var fileItem = await _permissionCheckingRepository.GetFileItemAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            var permission = await _permissionCheckingRepository.GetUserFilePermissionAsync(fileItemId, accountId);

            return new CurrentUserFilePermissionResponseDTO
            {
                CurrentUser = BuildCurrentUser(account),
                FileItemId = fileItem.Id,
                FileName = fileItem.Name,
                Permission = permission == null ? null : BuildFilePermissionItem(permission, fileItem.Name)
            };
        }

        private async Task<Account> GetAccountOrThrowAsync(Guid accountId)
        {
            return await _permissionCheckingRepository.GetAccountAsync(accountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);
        }

        private static CurrentUserDTO BuildCurrentUser(Account account) => new()
        {
            AccountId = account.Id,
            UserName = account.UserName,
            Email = account.Email
        };

        private static CurrentUserFolderPermissionItemDTO BuildFolderPermissionItem(
            FolderPermission fp, string folderName) => new()
        {
            PermissionId = fp.Id,
            FolderId = fp.FolderId,
            FolderName = folderName,
            ProjectParticipantId = fp.ProjectParticipantId,
            CanView = fp.CanView,
            CanEdit = fp.CanEdit,
            //CanUpdate = fp.CanUpdate,
            //CanDownload = fp.CanDownload,
            //CanVerify = fp.CanVerify,
            CanApprove = fp.CanApprove,
            Status = fp.Status
        };

        private static CurrentUserFilePermissionItemDTO BuildFilePermissionItem(
            FilePermission fp, string fileName) => new()
        {
            PermissionId = fp.Id,
            FileItemId = fp.FileItemId,
            FileName = fileName,
            ProjectParticipantId = fp.ProjectParticipantId,
            CanView = fp.CanView,
            CanEdit = fp.CanEdit,
            //CanUpdate = fp.CanUpdate,
            //CanDownload = fp.CanDownload,
            //CanVerify = fp.CanVerify,
            CanApprove = fp.CanApprove,
            Status = fp.Status
        };

        // ===== Shared evaluation =====

        /// <summary>
        /// System admin bypasses every check. Resolved here rather than at each call site so
        /// services that never receive an isSystemAdmin flag (FolderService, FileItemService…)
        /// still get the bypass, and no caller can forget to apply it.
        /// </summary>
        private async Task<bool> IsSystemAdminAsync(Guid accountId)
        {
            var account = await _permissionCheckingRepository.GetAccountAsync(accountId);
            return account?.Role == AccountRole.Admin;
        }

        private async Task CheckFolderAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission, string action)
        {
            if (await IsSystemAdminAsync(accountId)) return;

            if (!await HasFolderPermissionAsync(folderId, accountId, hasPermission))
                throw new ApiExceptionResponse(
                    $"You do not have '{action}' permission on this folder.", 403);
        }

        /// <summary>
        /// A FilePermission record is an override granted per file by an admin. Most files never
        /// get one (nothing creates them on upload), so when it is absent the decision defers to
        /// the folder that holds the file — the ACL that IS bootstrapped, in FolderBootstrapService.
        /// Present but denying wins: an explicit per-file record is not overridden by the folder.
        /// </summary>
        private async Task CheckFileAsync(
            Guid fileItemId,
            Guid accountId,
            Func<FilePermission, bool> hasFilePermission,
            Func<FolderPermission, bool> hasFolderPermission,
            string action)
        {
            if (await IsSystemAdminAsync(accountId)) return;

            var filePermission = await _permissionCheckingRepository
                .GetUserFilePermissionAsync(fileItemId, accountId);

            if (filePermission != null)
            {
                if (hasFilePermission(filePermission)) return;

                throw new ApiExceptionResponse(
                    $"You do not have '{action}' permission on this file.", 403);
            }

            var fileItem = await _permissionCheckingRepository.GetFileItemAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            if (await HasFolderPermissionAsync(fileItem.FolderId, accountId, hasFolderPermission)) return;

            throw new ApiExceptionResponse(
                $"You do not have '{action}' permission on this file.", 403);
        }

        private async Task<bool> HasFolderPermissionAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission)
        {
            var permission = await _permissionCheckingRepository
                .GetUserFolderPermissionAsync(folderId, accountId);

            return permission != null && hasPermission(permission);
        }
    }
}
