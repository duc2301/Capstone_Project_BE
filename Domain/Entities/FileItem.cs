using Domain.Enum.File;

using Domain.Common;

namespace Domain.Entities
{
    // 1 tài liệu trong thư mục CDE; nội dung thực + số version nằm ở các dòng FileVersionState
    public class FileItem : IEntity, IAuditable
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
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool? Warnning { get; set; } = false;
        public string? WarnningMessage { get; set; } = string.Empty;

        // Tóm tắt nội dung do AI sinh sau upload (worker ghi; null = chưa tóm tắt / không trích được chữ).
        public string? Description { get; set; }

        public Folder Folder { get; set; } = null!;
        public ICollection<FilePermission> Permissions { get; set; } = new List<FilePermission>();
        public ICollection<FileNamingMetadata> NamingMetadata { get; set; } = new List<FileNamingMetadata>();
    }
}
