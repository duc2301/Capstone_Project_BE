using Domain.Enum.Cde;



namespace Domain.Entities
{
    // Thư mục CDE: cây tự tham chiếu, gắn 1 khu vực CDE
    public class Folder 
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ParentFolderId { get; set; }
        public string Name { get; set; } = null!;
        public CdeArea Area { get; set; }
        public bool IsTemplate { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        // Nhóm (bên tham gia) SỞ HỮU folder — nhóm tạo ra nó. Leader nhóm khác được mời vào không
        // được gỡ/hạ quyền của nhóm chủ sở hữu (Admin/PM vẫn toàn quyền). NULL = folder cũ chưa gắn
        // chủ sở hữu (không được bảo vệ) hoặc folder hệ thống.
        public Guid? OwnerParticipantId { get; set; }
        // Folder gốc mà folder này là "bản chiếu" (mirror) ở khu vực khác — dùng để tra cứu
        // đích chuyển zone theo Id, không phụ thuộc tên (tránh vỡ liên kết khi đổi tên).
        public Guid? MirrorSourceFolderId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? NamingConventionId { get; set; }
        public Project Project { get; set; } = null!;
        public Folder? ParentFolder { get; set; }
        public ProjectParticipant? OwnerParticipant { get; set; }
        public ICollection<Folder> ChildFolders { get; set; } = new List<Folder>();
        public ICollection<FileItem> FileItems { get; set; } = new List<FileItem>();
        public ICollection<FolderPermission> Permissions { get; set; } = new List<FolderPermission>();
        public NamingConvention? NamingConvention { get; set; }
    }
}
