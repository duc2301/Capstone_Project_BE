namespace Application.DTOs.ResponseDTOs.Folder
{
    // 1 dòng ACL tường minh (override) trên folder, gán cho Group hoặc Organization.
    public class FolderPermissionResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? OrganizationId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDownload { get; set; }
        public bool CanVerify { get; set; }
        public bool CanApprove { get; set; }

        public bool InheritFromParent { get; set; }
    }
}
