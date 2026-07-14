namespace Application.DTOs.ResponseDTOs.PermissionChecking
{
    /// <summary>
    /// Feature 3 — the current user's permission on one specific file.
    /// Permission is null when the user has no permission record on the file.
    /// </summary>
    public class CurrentUserFilePermissionResponseDTO
    {
        public CurrentUserDTO CurrentUser { get; set; } = null!;
        public Guid FileItemId { get; set; }
        public string FileName { get; set; } = null!;
        public CurrentUserFilePermissionItemDTO? Permission { get; set; }
    }
}
