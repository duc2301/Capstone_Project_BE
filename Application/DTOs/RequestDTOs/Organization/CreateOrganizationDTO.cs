using System.ComponentModel.DataAnnotations;
using Domain.Enum.Department;

namespace Application.DTOs.RequestDTOs.Organization
{
    public class CreateOrganizationDTO
    {
        [Required]
        [StringLength(20)]
        public string TaxCode { get; set; } = null!;

        [Required]
        [StringLength(300)]
        public string LegalName { get; set; } = null!;

        [StringLength(300)]
        public string? DisplayName { get; set; }

        [Required]
        public DepartmentType Type { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [Phone]
        public string? Phone { get; set; }

        [EmailAddress]
        public string? Email { get; set; }
    }
}
