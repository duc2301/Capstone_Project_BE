namespace Application.DTOs.ResponseDTOs.PermissionChecking
{
    /// <summary>
    /// Feature 2 — the current user's permission on one specific folder.
    /// Permission is null when the user has no permission record on the folder.
    /// </summary>
    public class CurrentUserFolderPermissionResponseDTO
    {
        public CurrentUserDTO CurrentUser { get; set; } = null!;
        public Guid FolderId { get; set; }
        public string FolderName { get; set; } = null!;
        public CurrentUserFolderPermissionItemDTO? Permission { get; set; }
    }
}
