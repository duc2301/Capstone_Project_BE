using Domain.Common;
using Domain.Enum.File;

namespace Domain.Entities
{
    public class FileNote : IEntity, IAuditable
    {
        public Guid Id { get; set; }
        public Guid MarkupSetId { get; set; }
        public Guid FileVersionId { get; set; }
        public int? PageNumber { get; set; }
        public MarkupType MarkupType { get; set; }
        public string CoordinateJson { get; set; } = null!;
        public string? StyleJson { get; set; }
        public string? Content { get; set; }
        public string? ViewpointStateJson { get; set; }
        public string? MarkupSvg { get; set; }
        public string? ThumbnailDataUrl { get; set; }
        public FileNoteStatus Status { get; set; } = FileNoteStatus.Open;
        public Guid? AuthorAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public MarkupSet MarkupSet { get; set; } = null!;
        public FileVersion FileVersion { get; set; } = null!;
    }
}
