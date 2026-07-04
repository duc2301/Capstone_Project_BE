using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Markup
{
    public class CreateMarkupSetDTO
    {
        [Required]
        public Guid FileItemId { get; set; }

        public Guid? FileVersionId { get; set; }

        [StringLength(250)]
        public string? Title { get; set; }

        public Guid? IssueId { get; set; }
    }
}
