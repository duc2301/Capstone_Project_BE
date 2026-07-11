namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Centralized permission checking. Features call the matching method here
    /// before running their business logic instead of implementing their own checks.
    /// Each method throws a 403 ApiExceptionResponse with a universal message when denied.
    /// </summary>
    public interface IPermissionCheckingService
    {
        // Folder permissions
        Task CanViewFolderAsync(Guid folderId, Guid accountId);
        Task CanEditFolderAsync(Guid folderId, Guid accountId);
        Task CanUpdateFolderAsync(Guid folderId, Guid accountId);
        Task CanDownloadFolderAsync(Guid folderId, Guid accountId);
        Task CanVerifyFolderAsync(Guid folderId, Guid accountId);
        Task CanApproveFolderAsync(Guid folderId, Guid accountId);

        // File permissions
        Task CanViewFileAsync(Guid fileItemId, Guid accountId);
        Task CanEditFileAsync(Guid fileItemId, Guid accountId);
        Task CanUpdateFileAsync(Guid fileItemId, Guid accountId);
        Task CanDownloadFileAsync(Guid fileItemId, Guid accountId);
        Task CanVerifyFileAsync(Guid fileItemId, Guid accountId);
        Task CanApproveFileAsync(Guid fileItemId, Guid accountId);
    }
}
