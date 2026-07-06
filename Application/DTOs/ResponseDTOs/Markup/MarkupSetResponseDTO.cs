using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.Markup
{
    public class MarkupSetResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public Guid FileVersionId { get; set; }
        public int VersionNumber { get; set; }
        public string? Title { get; set; }
        public MarkupSetStatus Status { get; set; }
        public Guid? IssueId { get; set; }
        public string? SnapshotStoragePath { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int NoteCount { get; set; }
        public int OpenNoteCount { get; set; }
        public List<FileNoteResponseDTO> Notes { get; set; } = new();
    }
}
