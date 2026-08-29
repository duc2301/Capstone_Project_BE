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
    /// system admin, the project manager (Project.ManagerAccountId), or a ProjectAdmin (PM)
    /// bypasses everything; otherwise the GROUP ACL decides the ceiling — a group FilePermission
    /// record is an override (present -> it decides; absent, the normal case, falls back to the
    /// owning folder's ACL). A per-ACCOUNT override is then only a MASK on top of a group allow:
    /// it may lower/deny within the grant but can never grant standalone access (standalone
    /// per-user view goes through FileViewGrant / issue-stakeholder instead). No caching yet.
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
            => CheckFolderAsync(folderId, accountId, fp => fp.CanView, "Xem");

        public Task CanEditFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanEdit, "Chỉnh sửa");

        //public Task CanUpdateFolderAsync(Guid folderId, Guid accountId)
        //    => CheckFolderAsync(folderId, accountId, fp => fp.CanUpdate, "Cập nhật");

        public Task CanUploadToFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanEdit, "Chỉnh sửa");

        //public Task CanDownloadFolderAsync(Guid folderId, Guid accountId)
        //    => CheckFolderAsync(folderId, accountId, fp => fp.CanDownload, "Tải xuống");

        //public Task CanVerifyFolderAsync(Guid folderId, Guid accountId)
        //    => CheckFolderAsync(folderId, accountId, fp => fp.CanVerify, "Xác minh");

        public Task CanApproveFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanApprove, "Phê duyệt");

        // ===== File permissions =====

        // Mỗi method truyền 2 selector cùng nghĩa: một đọc cờ trên FilePermission (override riêng
        // của file), một đọc cờ tương ứng trên FolderPermission (dùng khi file chưa có override).

        public async Task CanViewFileAsync(Guid fileItemId, Guid accountId)
        {
            // Grant xem theo tài khoản (người được assign ký) là đường Allow cộng thêm — thắng cả
            // FilePermission override đang từ chối, nên kiểm tra trước khi rơi vào ACL nhóm.
            if (await _permissionCheckingRepository.HasActiveFileViewGrantAsync(fileItemId, accountId))
                return;
            if (await _permissionCheckingRepository.HasIssueStakeholderFileAccessAsync(fileItemId, accountId))
                return;
            await CheckFileAsync(fileItemId, accountId, fp => fp.CanView, fp => fp.CanView, "Xem");
        }

        public Task CanEditFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanEdit, fp => fp.CanEdit, "Chỉnh sửa");

        //public Task CanUpdateFileAsync(Guid fileItemId, Guid accountId)
        //    => CheckFileAsync(fileItemId, accountId, fp => fp.CanUpdate, fp => fp.CanUpdate, "Cập nhật");

        //public Task CanDownloadFileAsync(Guid fileItemId, Guid accountId)
        //    => CheckFileAsync(fileItemId, accountId, fp => fp.CanDownload, fp => fp.CanDownload, "Tải xuống");

        //public Task CanVerifyFileAsync(Guid fileItemId, Guid accountId)
        //    => CheckFileAsync(fileItemId, accountId, fp => fp.CanVerify, fp => fp.CanVerify, "Xác minh");

        public Task CanApproveFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanApprove, fp => fp.CanApprove, "Phê duyệt");

        // ===== Non-throwing checks (for callers that filter/branch instead of gating) =====

        public Task<bool> HasViewFolderAsync(Guid folderId, Guid accountId)
            => EvaluateFolderAsync(folderId, accountId, fp => fp.CanView);

        public Task<bool> HasEditFolderAsync(Guid folderId, Guid accountId)
            => EvaluateFolderAsync(folderId, accountId, fp => fp.CanEdit);

        public async Task<bool> HasViewFileAsync(Guid fileItemId, Guid accountId)
            => await _permissionCheckingRepository.HasActiveFileViewGrantAsync(fileItemId, accountId)
               || await _permissionCheckingRepository.HasIssueStakeholderFileAccessAsync(fileItemId, accountId)
               || await EvaluateFileAsync(fileItemId, accountId, fp => fp.CanView, fp => fp.CanView) == FileEval.Allowed;

        // Không có đường "grant cộng thêm" cho quyền GHI — FileViewGrant/issue chỉ cấp XEM.
        public async Task<bool> HasEditFileAsync(Guid fileItemId, Guid accountId)
            => await EvaluateFileAsync(fileItemId, accountId, fp => fp.CanEdit, fp => fp.CanEdit) == FileEval.Allowed;

        public async Task<HashSet<Guid>> GetEditableFileIdsAsync(
            Guid accountId, IReadOnlyCollection<Guid> fileItemIds)
        {
            var editable = new HashSet<Guid>();
            foreach (var fileId in fileItemIds)
            {
                if (await HasEditFileAsync(fileId, accountId))
                    editable.Add(fileId);
            }
            return editable;
        }

        public async Task<HashSet<Guid>> GetEditableFolderIdsAsync(
            Guid accountId, IReadOnlyCollection<Guid> folderIds)
        {
            var editable = new HashSet<Guid>();
            foreach (var folderId in folderIds)
            {
                if (await HasEditFolderAsync(folderId, accountId))
                    editable.Add(folderId);
            }
            return editable;
        }

        public async Task<HashSet<Guid>> GetViewableFolderIdsAmongAsync(
            Guid accountId, IReadOnlyCollection<Guid> folderIds)
        {
            var viewable = new HashSet<Guid>();
            foreach (var folderId in folderIds)
            {
                if (await HasViewFolderAsync(folderId, accountId))
                    viewable.Add(folderId);
            }
            return viewable;
        }

        public async Task<HashSet<Guid>> GetViewableFileIdsAmongAsync(
            Guid accountId, IReadOnlyCollection<Guid> fileItemIds)
        {
            var viewable = new HashSet<Guid>();
            foreach (var fileId in fileItemIds)
            {
                if (await HasViewFileAsync(fileId, accountId))
                    viewable.Add(fileId);
            }
            return viewable;
        }

        // ===== Project-scoped =====

        public async Task<bool> HasSystemAdminAsync(Guid accountId)
            => await IsSystemAdminAsync(accountId);

        public async Task<bool> HasProjectFullAccessAsync(Guid projectId, Guid accountId)
            => await IsSystemAdminAsync(accountId)
               || await _permissionCheckingRepository.IsProjectManagerAsync(projectId, accountId)
               || await _permissionCheckingRepository.HasProjectAdminAccessAsync(projectId, accountId);

        public Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId)
            => _permissionCheckingRepository.GetViewableFolderIdsAsync(projectId, accountId);

        public Task<HashSet<Guid>> GetExtraViewableFileIdsAsync(
            Guid projectId, Guid accountId, IReadOnlyCollection<Guid> viewableFolderIds)
            => _permissionCheckingRepository.GetExtraViewableFileIdsAsync(projectId, accountId, viewableFolderIds);

        public async Task<HashSet<Guid>> GetDeniedViewFileIdsInFolderAsync(Guid folderId, Guid accountId)
        {
            var denied = new HashSet<Guid>();

            // Files without a file-scoped override inherit the folder's ACL, so inside a folder the
            // caller has already confirmed is viewable they are always visible — no need to check
            // them. Only the override-bearing files can flip to denied, so evaluate just those, each
            // through HasViewFileAsync so the precedence (grants-win, account/group overrides) matches
            // the open-file check exactly.
            var candidateIds = await _permissionCheckingRepository
                .GetFileIdsWithActivePermissionByFolderAsync(folderId);

            foreach (var fileId in candidateIds)
            {
                if (!await HasViewFileAsync(fileId, accountId))
                    denied.Add(fileId);
            }

            return denied;
        }

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
        /// Allowed when: system admin, the project manager, or ProjectAdmin (PM) of the folder's
        /// project; otherwise the GROUP ACL is the CEILING — the user's active FolderPermission
        /// (group) must grant the requested flag. A per-account override (this folder or the nearest
        /// ancestor with one) is then a MASK: it may only LOWER within a group allow (a deny still
        /// hides the whole subtree, but subtractively), never grant standalone access. Standalone
        /// per-user grants go through FileViewGrant/issue-stakeholder, not this layer.
        /// </summary>
        private async Task<bool> EvaluateFolderAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission)
        {
            if (await IsSystemAdminAsync(accountId)) return true;
            if (await _permissionCheckingRepository.IsProjectManagerByFolderAsync(folderId, accountId)) return true;
            if (await _permissionCheckingRepository.HasProjectAdminAccessByFolderAsync(folderId, accountId)) return true;

            // Group ACL = ceiling. No group grant -> no access; the override layer cannot add any.
            if (!await EvaluateFolderGroupAsync(folderId, accountId, hasPermission))
                return false;

            // Mask: nearest-ancestor account override may only refine (lower) the group allow.
            var accountOverride = await _permissionCheckingRepository
                .GetNearestFolderAccountOverrideByFolderAsync(folderId, accountId);
            if (accountOverride != null) return hasPermission(accountOverride);

            return true;
        }

        /// <summary>
        /// Group-only folder ACL, with the full-access bypass and the per-account override layer
        /// already resolved by the caller. Kept separate so the file fallback does not re-run the
        /// bypass checks or the ancestor override walk it already performed.
        /// </summary>
        private async Task<bool> EvaluateFolderGroupAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission)
        {
            var permission = await _permissionCheckingRepository
                .GetUserFolderPermissionAsync(folderId, accountId);

            return permission != null && hasPermission(permission);
        }

        private enum FileEval { Allowed, Denied, NotFound }

        /// <summary>
        /// Full-access bypass first (system admin / project manager / PM of the file's project).
        /// Then the GROUP decision is the CEILING: a group FilePermission record acts as a per-file
        /// override (present -> it decides, present-but-denying wins); absent -> the owning folder's
        /// group ACL, the one that IS bootstrapped. If the group denies, the account-override layer
        /// is never consulted — an account override can no longer grant standalone access.
        /// Only within a group allow does the per-account layer (Google-Drive style) apply as a MASK
        /// that may lower the grant: the file's own account override first (most specific wins), then
        /// the nearest ancestor folder's account override (so a folder deny still subtracts from the
        /// whole subtree, files included).
        /// Note: the additive view grants (FileViewGrant / issue stakeholder) are resolved before this
        /// method in CanViewFileAsync/HasViewFileAsync, so a required signer is never hidden by a deny.
        /// </summary>
        private async Task<FileEval> EvaluateFileAsync(
            Guid fileItemId,
            Guid accountId,
            Func<FilePermission, bool> hasFilePermission,
            Func<FolderPermission, bool> hasFolderPermission)
        {
            if (await IsSystemAdminAsync(accountId)) return FileEval.Allowed;
            if (await _permissionCheckingRepository.IsProjectManagerByFileAsync(fileItemId, accountId))
                return FileEval.Allowed;
            if (await _permissionCheckingRepository.HasProjectAdminAccessByFileAsync(fileItemId, accountId))
                return FileEval.Allowed;

            // Group ceiling: file group override present-wins, else the owning folder's group ACL.
            var filePermission = await _permissionCheckingRepository
                .GetUserFilePermissionAsync(fileItemId, accountId);

            bool groupAllows;
            if (filePermission != null)
            {
                groupAllows = hasFilePermission(filePermission);
            }
            else
            {
                var fileItem = await _permissionCheckingRepository.GetFileItemAsync(fileItemId);
                if (fileItem == null) return FileEval.NotFound;
                groupAllows = await EvaluateFolderGroupAsync(fileItem.FolderId, accountId, hasFolderPermission);
            }

            if (!groupAllows) return FileEval.Denied;

            // Mask: account overrides may only refine (lower) the group allow. The file's own
            // override is more specific than an ancestor folder's, so it is consulted first.
            var fileAccountOverride = await _permissionCheckingRepository
                .GetUserFileAccountOverrideAsync(fileItemId, accountId);
            if (fileAccountOverride != null)
                return hasFilePermission(fileAccountOverride) ? FileEval.Allowed : FileEval.Denied;

            var folderAccountOverride = await _permissionCheckingRepository
                .GetNearestFolderAccountOverrideByFileAsync(fileItemId, accountId);
            if (folderAccountOverride != null)
                return hasFolderPermission(folderAccountOverride) ? FileEval.Allowed : FileEval.Denied;

            return FileEval.Allowed;
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
                    throw new ApiExceptionResponse("Không tìm thấy tệp.", 404);
                default:
                    throw new ApiExceptionResponse(
                        $"Bạn không có quyền '{action}' đối với tệp này.", 403);
            }
        }
    }
}
