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
    }
}
