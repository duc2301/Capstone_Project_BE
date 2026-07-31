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

        [StringLength(50)]
        public string? ProjectCode { get; set; }
        public string? ProjectImageUrl { get; set; }

        public Guid? ManagerAccountId { get; set; }
        public ProjectStatus? Status { get; set; }

        public Guid? OwnerOrganizationId { get; set; }

        [StringLength(500)]
        public string? ContactAddress { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
