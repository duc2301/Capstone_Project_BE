using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    /// <summary>
    /// Data access for centralized permission checking.
    /// Only retrieves permission records — no business logic here.
    /// </summary>
    public interface IPermissionCheckingRepository
    {
        /// <summary>
        /// Find the user's active project participant on the folder's project
        /// and return the corresponding active FolderPermission record (null if none).
        /// </summary>
        Task<FolderPermission?> GetUserFolderPermissionAsync(Guid folderId, Guid accountId);

        /// <summary>
        /// Find the user's active project participant on the file's project
        /// and return the corresponding active FilePermission record (null if none).
        /// </summary>
        Task<FilePermission?> GetUserFilePermissionAsync(Guid fileItemId, Guid accountId);

        // ===== Per-account overrides (Google-Drive style user grant/deny) =====
        // These target FilePermission/FolderPermission rows keyed by AccountId (not a group).
        // An active row is an OVERRIDE that decides on its own: CanView=false = an explicit deny
        // that wins over the group ACL. Absent = no opinion (fall through to the group ACL).

        /// <summary>
        /// The account's own active override row on this file (AccountId-keyed), or null.
        /// </summary>
        Task<FilePermission?> GetUserFileAccountOverrideAsync(Guid fileItemId, Guid accountId);

        /// <summary>
        /// Walking up from the file's owning folder (inclusive), the nearest folder that carries an
        /// active per-account override for this user, or null. Lets a folder-level grant/deny apply
        /// to the whole subtree, including files added later.
        /// </summary>
        Task<FolderPermission?> GetNearestFolderAccountOverrideByFileAsync(Guid fileItemId, Guid accountId);

        /// <summary>
        /// Walking up from this folder (inclusive), the nearest folder that carries an active
        /// per-account override for this user, or null.
        /// </summary>
        Task<FolderPermission?> GetNearestFolderAccountOverrideByFolderAsync(Guid folderId, Guid accountId);

        // ===== Project-admin (PM) full access =====
        // Reimplemented here rather than reused from FolderTreeRepository so the permission module
        // owns its own data access (FolderTreeService keeps its own copies of these queries).

        /// <summary>True if the account is an active ProjectAdmin (PM) participant of the project.</summary>
        Task<bool> HasProjectAdminAccessAsync(Guid projectId, Guid accountId);

        /// <summary>True if the account is an active ProjectAdmin of the project that owns the folder.</summary>
        Task<bool> HasProjectAdminAccessByFolderAsync(Guid folderId, Guid accountId);

        /// <summary>True if the account is an active ProjectAdmin of the project that owns the file.</summary>
        Task<bool> HasProjectAdminAccessByFileAsync(Guid fileItemId, Guid accountId);

        // ===== Project manager (Project.ManagerAccountId) full access =====
        // The single account assigned as project manager. Treated like a system admin (full bypass,
        // including the WIP area) but scoped to the manager's own project.

        /// <summary>True if the account is the manager (Project.ManagerAccountId) of the project.</summary>
        Task<bool> IsProjectManagerAsync(Guid projectId, Guid accountId);

        /// <summary>True if the account is the manager of the project that owns the folder.</summary>
        Task<bool> IsProjectManagerByFolderAsync(Guid folderId, Guid accountId);

        /// <summary>True if the account is the manager of the project that owns the file.</summary>
        Task<bool> IsProjectManagerByFileAsync(Guid fileItemId, Guid accountId);

        // ===== Owner-group leadership (permission-assignment authority) =====

        /// <summary>
        /// Among the given folders, the FolderIds whose OWNING participant (Folder.OwnerParticipantId,
        /// active) is a group the account leads (active GroupMember with Role == Leader). Folders with
        /// no owner are never returned — only Admin/PM can assign there. Single query.
        /// </summary>
        Task<HashSet<Guid>> GetLeaderOwnedFolderIdsAmongAsync(Guid accountId, IReadOnlyCollection<Guid> folderIds);

        /// <summary>
        /// FolderIds in the project the account can View
        /// (active GroupMember -> active ProjectParticipant -> active FolderPermission with CanView).
        /// </summary>
        Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId);

        /// <summary>
        /// FileItemIds the account can view through a FILE-level grant while the owning folder is NOT
        /// in viewableFolderIds — the files a folder-only filter would wrongly drop. Covers every
        /// additive path HasViewFileAsync accepts: a CanView FilePermission (group or per-account),
        /// an active FileViewGrant, and open-issue stakeholder access.
        /// Files inside already-viewable folders are excluded: the folder filter admits them anyway.
        /// </summary>
        Task<HashSet<Guid>> GetExtraViewableFileIdsAsync(
            Guid projectId, Guid accountId, IReadOnlyCollection<Guid> viewableFolderIds);

        /// <summary>
        /// True if the account holds an active per-account view grant on the file (FileViewGrant),
        /// issued because they were assigned to sign it. Independent of the group-based ACL.
        /// </summary>
        Task<bool> HasActiveFileViewGrantAsync(Guid fileItemId, Guid accountId);

        Task<bool> HasIssueStakeholderFileAccessAsync(Guid fileItemId, Guid accountId);

        /// <summary>
        /// Distinct FileItemIds of files in the folder that carry at least one active FilePermission
        /// row (a group file override or a per-account override). These are the only files whose view
        /// access can diverge from the owning folder's ACL, so a listing filter evaluates just these
        /// instead of every file in the folder.
        /// </summary>
        Task<List<Guid>> GetFileIdsWithActivePermissionByFolderAsync(Guid folderId);

        // ===== Current-user permission retrieval (viewing only) =====

        Task<Account?> GetAccountAsync(Guid accountId);

        Task<Folder?> GetFolderAsync(Guid folderId);

        Task<FileItem?> GetFileItemAsync(Guid fileItemId);

        /// <summary>Active group memberships of the account, with the Group included.</summary>
        Task<List<GroupMember>> GetActiveGroupMembershipsAsync(Guid accountId);

        /// <summary>Active project participants of the given groups, with the Project included.</summary>
        Task<List<ProjectParticipant>> GetActiveParticipantsByGroupIdsAsync(List<Guid> groupIds);

        /// <summary>All folder permission records of the given participants, with the Folder included.</summary>
        Task<List<FolderPermission>> GetFolderPermissionsByParticipantIdsAsync(List<Guid> participantIds);

        /// <summary>All file permission records of the given participants, with the FileItem included.</summary>
        Task<List<FilePermission>> GetFilePermissionsByParticipantIdsAsync(List<Guid> participantIds);
    }
}
