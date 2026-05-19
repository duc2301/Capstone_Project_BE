using System.ComponentModel.DataAnnotations;
using Domain.Enum.Department;

namespace Application.DTOs.RequestDTOs.Organization
{
    public class UpdateOrganizationDTO
    {
        [StringLength(20)]
        public string? TaxCode { get; set; }

        [StringLength(300)]
        public string? LegalName { get; set; }

        [StringLength(300)]
        public string? DisplayName { get; set; }

        public DepartmentType? Type { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [Phone]
        public string? Phone { get; set; }

        [EmailAddress]
        public string? Email { get; set; }
    }
}
