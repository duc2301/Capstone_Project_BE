using Domain.Common;
using Domain.Enum.File;

namespace Domain.Entities
{
    public class MarkupSet : IEntity, IAuditable
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public Guid FileVersionId { get; set; }
        public string? Title { get; set; }
        public MarkupSetStatus Status { get; set; } = MarkupSetStatus.Open;
        public Guid? IssueId { get; set; }
        public string? SnapshotStoragePath { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public FileItem FileItem { get; set; } = null!;
        // FK trỏ sang FileVersionStates (hệ versioning mới) — FileVersions cũ đang được gỡ bỏ
        public FileVersionState FileVersion { get; set; } = null!;
        public ICollection<FileNote> Notes { get; set; } = new List<FileNote>();
    }
}
