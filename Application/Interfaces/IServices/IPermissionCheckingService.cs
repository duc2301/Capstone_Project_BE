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

        /// <summary>
        /// True if the account has EDIT (Write) on the file, by the same precedence as the throwing
        /// CanEditFileAsync gate (bypass, per-account override, ancestor-folder override, group file
        /// override, group folder ACL). No additive grant path — view grants do not confer edit.
        /// </summary>
        Task<bool> HasEditFileAsync(Guid fileItemId, Guid accountId);

        /// <summary>
        /// Among the given files, the FileItemIds this account has EDIT (Write) on — for filtering a
        /// list to the files a non-admin/PM caller may actually manage. Decided through
        /// HasEditFileAsync, so the full override precedence is never re-derived.
        /// </summary>
        Task<HashSet<Guid>> GetEditableFileIdsAsync(Guid accountId, IReadOnlyCollection<Guid> fileItemIds);

        /// <summary>
        /// Among the given folders, the FolderIds this account has EDIT (Write) on — for filtering a
        /// list to the folders a non-admin/PM caller may actually manage. Decided through
        /// HasEditFolderAsync (no downward inheritance — write on a parent does not confer it here).
        /// </summary>
        Task<HashSet<Guid>> GetEditableFolderIdsAsync(Guid accountId, IReadOnlyCollection<Guid> folderIds);

        /// <summary>
        /// Among the given folders, the FolderIds this account can VIEW — the read-gated counterpart of
        /// GetEditableFolderIdsAsync, decided through HasViewFolderAsync. (Distinct from the
        /// project-wide GetViewableFolderIdsAsync: this filters a caller-supplied id set.)
        /// </summary>
        Task<HashSet<Guid>> GetViewableFolderIdsAmongAsync(Guid accountId, IReadOnlyCollection<Guid> folderIds);

        /// <summary>
        /// Among the given files, the FileItemIds this account can VIEW — the read-gated counterpart of
        /// GetEditableFileIdsAsync, decided through HasViewFileAsync.
        /// </summary>
        Task<HashSet<Guid>> GetViewableFileIdsAmongAsync(Guid accountId, IReadOnlyCollection<Guid> fileItemIds);

        // ===== Project-scoped =====
        /// <summary>True if the account is a system admin.</summary>
        Task<bool> HasSystemAdminAsync(Guid accountId);

        /// <summary>True if the account has full access to the project (system admin or ProjectAdmin/PM).</summary>
        Task<bool> HasProjectFullAccessAsync(Guid projectId, Guid accountId);

        // ===== Permission-assignment authority (ownership-based) =====
        // Assigning permissions on a folder/file is restricted to the LEADER of the owning group,
        // plus project full-access (system admin / PM / ProjectAdmin). Distinct from view/edit: an
        // invited group with view/edit can operate in the folder but cannot assign any permission.

        /// <summary>
        /// True if the account may assign permissions on this folder: project full-access, OR the
        /// leader of the group that owns it (Folder.OwnerParticipantId). Owner-less folders -> only
        /// full-access qualifies.
        /// </summary>
        Task<bool> CanAssignFolderPermissionsAsync(Guid folderId, Guid accountId);

        /// <summary>
        /// True if the account may assign permissions on this file — decided by the OWNING folder's
        /// ownership (files have no owner of their own), so file and folder assignment rules match.
        /// </summary>
        Task<bool> CanAssignFilePermissionsAsync(Guid fileItemId, Guid accountId);

        /// <summary>
        /// Among the given folders, those a NON-full-access caller may assign on (leads the owning
        /// group). For building the matrix's editable set; full-access callers bypass this entirely.
        /// </summary>
        Task<HashSet<Guid>> GetAssignableFolderIdsAmongAsync(Guid accountId, IReadOnlyCollection<Guid> folderIds);

        /// <summary>FolderIds in the project the account can View — for building filtered list views.</summary>
        Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId);

        /// <summary>
        /// FileItemIds viewable through a FILE-level grant while the owning folder is NOT viewable —
        /// the files a folder-only filter drops even though the user may open them. Callers that
        /// pre-filter by folder (semantic search) must widen their candidate set with this, otherwise
        /// "cấp quyền cho riêng một tệp" works everywhere except there.
        /// </summary>
        Task<HashSet<Guid>> GetExtraViewableFileIdsAsync(
            Guid projectId, Guid accountId, IReadOnlyCollection<Guid> viewableFolderIds);

        /// <summary>
        /// Among the files in one folder, the FileItemIds this account is DENIED view on — for
        /// filtering a folder listing so a file blocked at the file level (group or per-account
        /// override) stops appearing, not just stops opening. Decided through HasViewFileAsync (the
        /// single authority), so grants-win and the full override precedence are never re-derived.
        /// </summary>
        Task<HashSet<Guid>> GetDeniedViewFileIdsInFolderAsync(Guid folderId, Guid accountId);

        // ===== Current-user permission retrieval (viewing only, no authorization) =====

        /// <summary>Every folder/file permission the current user has, with the full user -> group -> participant chain.</summary>
        Task<CurrentUserPermissionsResponseDTO> GetCurrentUserPermissionsAsync(Guid accountId);

        /// <summary>The current user's permission on one specific folder.</summary>
        Task<CurrentUserFolderPermissionResponseDTO> GetCurrentUserFolderPermissionAsync(Guid folderId, Guid accountId);

        /// <summary>The current user's permission on one specific file.</summary>
        Task<CurrentUserFilePermissionResponseDTO> GetCurrentUserFilePermissionAsync(Guid fileItemId, Guid accountId);
    }
}
