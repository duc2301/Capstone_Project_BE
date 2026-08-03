using Application.DTOs.ResponseDTOs.PermissionChecking;

namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Centralized permission checking. Features call the matching method here
    /// before running their business logic instead of implementing their own checks.
    /// Can* methods throw a 403 ApiExceptionResponse with a universal message when denied;
    /// Has* methods return a bool for callers that filter or branch instead of gating.
    /// System admins and project admins (PMs) bypass every check.
    /// </summary>
    public interface IPermissionCheckingService
    {
        // Folder permissions
        Task CanViewFolderAsync(Guid folderId, Guid accountId);
        Task CanEditFolderAsync(Guid folderId, Guid accountId);
        //Task CanUpdateFolderAsync(Guid folderId, Guid accountId);
        //Task CanDownloadFolderAsync(Guid folderId, Guid accountId);
        //Task CanVerifyFolderAsync(Guid folderId, Guid accountId);
        Task CanApproveFolderAsync(Guid folderId, Guid accountId);

        Task CanUploadToFolderAsync(Guid folderId, Guid accountId);

        // File permissions.
        // A FilePermission record overrides the folder; when the file has none — the normal case,
        // since nothing creates them on upload — the check falls back to the owning folder's ACL.
        Task CanViewFileAsync(Guid fileItemId, Guid accountId);
        Task CanEditFileAsync(Guid fileItemId, Guid accountId);
        //Task CanUpdateFileAsync(Guid fileItemId, Guid accountId);
        //Task CanDownloadFileAsync(Guid fileItemId, Guid accountId);
        //Task CanVerifyFileAsync(Guid fileItemId, Guid accountId);
        Task CanApproveFileAsync(Guid fileItemId, Guid accountId);

        // ===== Non-throwing checks (for callers that filter a list or branch on access) =====
        Task<bool> HasViewFolderAsync(Guid folderId, Guid accountId);
        Task<bool> HasEditFolderAsync(Guid folderId, Guid accountId);
        Task<bool> HasViewFileAsync(Guid fileItemId, Guid accountId);

        // ===== Project-scoped =====
        /// <summary>True if the account is a system admin.</summary>
        Task<bool> HasSystemAdminAsync(Guid accountId);

        /// <summary>True if the account has full access to the project (system admin or ProjectAdmin/PM).</summary>
        Task<bool> HasProjectFullAccessAsync(Guid projectId, Guid accountId);

        /// <summary>FolderIds in the project the account can View — for building filtered list views.</summary>
        Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId);

        // ===== Current-user permission retrieval (viewing only, no authorization) =====

        /// <summary>Every folder/file permission the current user has, with the full user -> group -> participant chain.</summary>
        Task<CurrentUserPermissionsResponseDTO> GetCurrentUserPermissionsAsync(Guid accountId);

        /// <summary>The current user's permission on one specific folder.</summary>
        Task<CurrentUserFolderPermissionResponseDTO> GetCurrentUserFolderPermissionAsync(Guid folderId, Guid accountId);

        /// <summary>The current user's permission on one specific file.</summary>
        Task<CurrentUserFilePermissionResponseDTO> GetCurrentUserFilePermissionAsync(Guid fileItemId, Guid accountId);
    }
}
