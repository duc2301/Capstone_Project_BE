using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.ProjectModel
{
    public class CreateProjectModelDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        [StringLength(2000)]
        public string? Description { get; set; }
    }
}
