using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.Markup
{
    public class FileNoteResponseDTO : IResponseDto
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
        public FileNoteStatus Status { get; set; }
        public Guid? AuthorAccountId { get; set; }
        public string? AuthorName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
