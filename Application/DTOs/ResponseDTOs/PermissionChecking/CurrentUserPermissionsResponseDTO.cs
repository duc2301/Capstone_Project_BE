namespace Application.DTOs.ResponseDTOs.PermissionChecking
{
    /// <summary>
    /// Feature 1 — everything the current user has:
    /// user -> groups -> project participants -> folder/file permissions.
    /// </summary>
    public class CurrentUserPermissionsResponseDTO
    {
        public CurrentUserDTO CurrentUser { get; set; } = null!;
        public List<CurrentUserGroupDTO> Groups { get; set; } = new();
        public List<CurrentUserParticipantDTO> ProjectParticipants { get; set; } = new();
        public CurrentUserPermissionListDTO Permissions { get; set; } = new();
    }

    public class CurrentUserPermissionListDTO
    {
        public List<CurrentUserFolderPermissionItemDTO> FolderPermissions { get; set; } = new();
        public List<CurrentUserFilePermissionItemDTO> FilePermissions { get; set; } = new();
    }
}
