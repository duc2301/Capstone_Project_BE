using Application.DTOs.ResponseDTOs.PermissionChecking;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Domain.Entities;
using Domain.Enum.Account;

namespace Application.Services
{
    /// <summary>
    /// Centralized permission evaluation and the single source of ACL decisions for feature
    /// services (FolderTreeService keeps its own folder-tree queries). Flow:
    /// system admin or ProjectAdmin (PM) bypasses everything; otherwise look up the user's
    /// permission record and check the requested flag. A FilePermission record is an OVERRIDE —
    /// when a file has none (the normal case, since they are only created by explicit admin
    /// action), the check falls back to the owning folder's ACL. No caching yet.
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

        // ===== Non-throwing checks (for callers that filter/branch instead of gating) =====

        public Task<bool> HasViewFolderAsync(Guid folderId, Guid accountId)
            => EvaluateFolderAsync(folderId, accountId, fp => fp.CanView);

        public Task<bool> HasEditFolderAsync(Guid folderId, Guid accountId)
            => EvaluateFolderAsync(folderId, accountId, fp => fp.CanEdit);

        public async Task<bool> HasViewFileAsync(Guid fileItemId, Guid accountId)
            => await EvaluateFileAsync(fileItemId, accountId, fp => fp.CanView, fp => fp.CanView) == FileEval.Allowed;

        // ===== Project-scoped =====

        public async Task<bool> HasSystemAdminAsync(Guid accountId)
            => await IsSystemAdminAsync(accountId);

        public async Task<bool> HasProjectFullAccessAsync(Guid projectId, Guid accountId)
            => await IsSystemAdminAsync(accountId)
               || await _permissionCheckingRepository.HasProjectAdminAccessAsync(projectId, accountId);

        public Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId)
            => _permissionCheckingRepository.GetViewableFolderIdsAsync(projectId, accountId);

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

        // ===== Full-access bypass =====

        /// <summary>
        /// System admin bypasses every check. Resolved here rather than at each call site so
        /// services that never receive an isSystemAdmin flag still get the bypass, and no caller
        /// can forget to apply it.
        /// </summary>
        private async Task<bool> IsSystemAdminAsync(Guid accountId)
        {
            var account = await _permissionCheckingRepository.GetAccountAsync(accountId);
            return account?.Role == AccountRole.Admin;
        }

        // ===== Shared evaluation (returns bool; throwing gates wrap these) =====

        /// <summary>
        /// Allowed when: system admin, or ProjectAdmin (PM) of the folder's project, or the user's
        /// active FolderPermission grants the requested flag.
        /// </summary>
        private async Task<bool> EvaluateFolderAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission)
        {
            if (await IsSystemAdminAsync(accountId)) return true;
            if (await _permissionCheckingRepository.HasProjectAdminAccessByFolderAsync(folderId, accountId)) return true;

            var permission = await _permissionCheckingRepository
                .GetUserFolderPermissionAsync(folderId, accountId);

            return permission != null && hasPermission(permission);
        }

        private enum FileEval { Allowed, Denied, NotFound }

        /// <summary>
        /// Full-access bypass first (system admin / PM of the file's project). Then a FilePermission
        /// record acts as a per-file override: present -> it decides (present-but-denying wins);
        /// absent -> defer to the owning folder's ACL, the one that IS bootstrapped.
        /// </summary>
        private async Task<FileEval> EvaluateFileAsync(
            Guid fileItemId,
            Guid accountId,
            Func<FilePermission, bool> hasFilePermission,
            Func<FolderPermission, bool> hasFolderPermission)
        {
            if (await IsSystemAdminAsync(accountId)) return FileEval.Allowed;
            if (await _permissionCheckingRepository.HasProjectAdminAccessByFileAsync(fileItemId, accountId))
                return FileEval.Allowed;

            var filePermission = await _permissionCheckingRepository
                .GetUserFilePermissionAsync(fileItemId, accountId);

            if (filePermission != null)
                return hasFilePermission(filePermission) ? FileEval.Allowed : FileEval.Denied;

            var fileItem = await _permissionCheckingRepository.GetFileItemAsync(fileItemId);
            if (fileItem == null) return FileEval.NotFound;

            return await EvaluateFolderAsync(fileItem.FolderId, accountId, hasFolderPermission)
                ? FileEval.Allowed : FileEval.Denied;
        }

        // ===== Throwing gates =====

        private async Task CheckFolderAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission, string action)
        {
            if (!await EvaluateFolderAsync(folderId, accountId, hasPermission))
                throw new ApiExceptionResponse(
                    $"You do not have '{action}' permission on this folder.", 403);
        }

        private async Task CheckFileAsync(
            Guid fileItemId,
            Guid accountId,
            Func<FilePermission, bool> hasFilePermission,
            Func<FolderPermission, bool> hasFolderPermission,
            string action)
        {
            switch (await EvaluateFileAsync(fileItemId, accountId, hasFilePermission, hasFolderPermission))
            {
                case FileEval.Allowed:
                    return;
                case FileEval.NotFound:
                    throw new ApiExceptionResponse("File not found.", 404);
                default:
                    throw new ApiExceptionResponse(
                        $"You do not have '{action}' permission on this file.", 403);
            }
        }
    }
}
