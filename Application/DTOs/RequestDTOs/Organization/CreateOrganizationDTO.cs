using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Organization
{
    public class CreateOrganizationDTO
    {
        [StringLength(20)]
        public string? TaxCode { get; set; }

        [Required]
        [StringLength(300)]
        public string LegalName { get; set; } = null!;

        [StringLength(300)]
        public string? DisplayName { get; set; }

        [Required]
        public Guid OrganizationTypeId { get; set; }

        public string? AvatarUrl { get; set; }
        public bool IsJointVenture { get; set; }
        public Guid? RepresentativeOrganizationId { get; set; }
        public List<Guid>? JointVentureMemberIds { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [Phone]
        public string? Phone { get; set; }

        [EmailAddress]
        public string? Email { get; set; }
    }
}
