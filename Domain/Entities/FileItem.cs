using Domain.Enum.File;
namespace Domain.Entities
{
    // 1 tài liệu trong thư mục CDE; nội dung thực + số version nằm ở các dòng FileVersionState
    public class FileItem 
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public string Name { get; set; } = null!;
        public FileType FileType { get; set; }
        public FileItemStatus Status { get; set; } = FileItemStatus.Draft;
        public bool RequiresSignature { get; set; }
        public bool IsSigned { get; set; }
        public Guid? CurrentVersionId { get; set; }
        public Guid? SignedVersionId { get; set; }
        public Guid? CreatedByAccountId { get; set; }

        // Bản lưu (mirror) trong vùng Archived: trỏ về file Published gốc đã niêm phong.
        // NULL với file thường. Dùng để cộng dồn các phiên bản chính thức qua từng lần niêm phong.
        public Guid? SourceFileItemId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Folder Folder { get; set; } = null!;
        public ICollection<FilePermission> Permissions { get; set; } = new List<FilePermission>();
        public ICollection<FileNamingMetadata> NamingMetadata { get; set; } = new List<FileNamingMetadata>();
    }
}
