using System.ComponentModel.DataAnnotations;
using Domain.Enum.Project;

namespace Application.DTOs.RequestDTOs.Project
{
    public class CreateProjectDTO
    {
        [Required]
        [StringLength(250)]
        public string ProjectName { get; set; } = null!;

        [StringLength(2000)]
        public string? ProjectDescription { get; set; }

        public string? ProjectCode { get; set; }
        public string? ProjectImageUrl { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
        public ProjectPhase Phase { get; set; } = ProjectPhase.Concept;

        [StringLength(500)]
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
