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
