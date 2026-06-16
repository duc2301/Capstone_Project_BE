namespace Domain.Entities
{
    // ACL trên thư mục, gán cho Nhóm hoặc Tổ chức.
    // 5 cờ trong tài liệu + Xem ngầm định. Workflow phiếu yêu cầu suy quyền từ đây.
    public class FolderPermission
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public Guid? ProjectParticipantId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }       // Sửa
        public bool CanUpdate { get; set; }     // Cập nhật (upload phiên bản)
        public bool CanDownload { get; set; }   // Tải về
        public bool CanVerify { get; set; }     // Thẩm tra
        public bool CanApprove { get; set; }    // Duyệt

        public bool InheritFromParent { get; set; }

        public Folder Folder { get; set; } = null!;
        public ProjectParticipant? ProjectParticipant { get; set; }
    }
}
