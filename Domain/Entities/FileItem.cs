using Domain.Enum.File;

using Domain.Common;

namespace Domain.Entities
{
    // 1 tài liệu trong thư mục CDE; nội dung thực nằm ở các FileVersion
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

        public Folder Folder { get; set; } = null!;
        public ICollection<FileVersion> Versions { get; set; } = new List<FileVersion>();
        public ICollection<FilePermission> Permissions { get; set; } = new List<FilePermission>();
    }
}
