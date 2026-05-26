using System.ComponentModel.DataAnnotations;
using Domain.Enum.Project;

namespace Application.DTOs.RequestDTOs.Project
{
    public class UpdateProjectDTO
    {
        [StringLength(250)]
        public string? ProjectName { get; set; }

        [StringLength(2000)]
        public string? ProjectDescription { get; set; }

        public Guid? ManagerAccountId { get; set; }
        public ProjectStatus? Status { get; set; }
        public ProjectPhase? Phase { get; set; }
    }
}
