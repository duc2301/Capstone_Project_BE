using System.ComponentModel.DataAnnotations;
using Domain.Enum.File;

namespace Application.DTOs.RequestDTOs.Markup
{
    public class UpdateFileNoteDTO
    {
        public MarkupType? MarkupType { get; set; }
        public int? PageNumber { get; set; }
        public string? CoordinateJson { get; set; }
        public string? StyleJson { get; set; }
        [StringLength(4000)]
        public string? Content { get; set; }
        public FileNoteStatus? Status { get; set; }
    }
}
