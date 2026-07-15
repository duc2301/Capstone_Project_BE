using System.ComponentModel.DataAnnotations;
using Domain.Enum.File;

namespace Application.DTOs.RequestDTOs.Markup
{
    public class CreateFileNoteDTO
    {
        [Required]
        public MarkupType MarkupType { get; set; }

        public int? PageNumber { get; set; }

        public string? CoordinateJson { get; set; }

        public string? StyleJson { get; set; }

        [StringLength(4000)]
        public string? Content { get; set; }

        public string? ViewpointStateJson { get; set; }

        public string? MarkupSvg { get; set; }

        public string? ThumbnailDataUrl { get; set; }
    }
}
