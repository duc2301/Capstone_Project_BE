using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Project
{
    public class CreateProjectDTO
    {
        [Required]
        [StringLength(250)]
        public string ProjectName { get; set; } = null!;

        [StringLength(2000)]
        public string? ProjectDescription { get; set; }

        [Required]
        public Guid DepartmentId { get; set; }
    }
}
