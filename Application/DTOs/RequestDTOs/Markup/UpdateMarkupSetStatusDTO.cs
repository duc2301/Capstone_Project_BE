using System.ComponentModel.DataAnnotations;
using Domain.Enum.File;

namespace Application.DTOs.RequestDTOs.Markup
{
    public class UpdateMarkupSetStatusDTO
    {
        [Required]
        public MarkupSetStatus Status { get; set; }
    }
}
