using Domain.Enum.Permission;

namespace Application.DTOs.ResponseDTOs.PermissionChecking
{
    public class CurrentUserFolderPermissionItemDTO
    {
        public Guid PermissionId { get; set; }
        public Guid FolderId { get; set; }
        public string FolderName { get; set; } = null!;
        public Guid? ProjectParticipantId { get; set; }
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDownload { get; set; }
        public bool CanVerify { get; set; }
        public bool CanApprove { get; set; }
        public PermissionStatus Status { get; set; }
    }
}
