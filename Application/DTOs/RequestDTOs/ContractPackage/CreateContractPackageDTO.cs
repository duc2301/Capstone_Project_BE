using System.ComponentModel.DataAnnotations;
using Domain.Enum.ContractPackage;

namespace Application.DTOs.RequestDTOs.ContractPackage
{
    public class CreateContractPackageDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [StringLength(50)]
        public string? Code { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        [StringLength(2000)]
        public string? Description { get; set; }

        public decimal? ContractValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [Required]
        public PackageStatus Status { get; set; }

        public bool IsDefault { get; set; }

        public string? WorkTypes { get; set; }
        public string? ScopeDescription { get; set; }
        public decimal? TaxRate { get; set; }
        public string? Currency { get; set; }
        public string? Notes { get; set; }
        public Guid? DocumentFolderId { get; set; }

        public Guid? ContractorOrganizationId { get; set; }
        public Guid? RepresentativeAccountId { get; set; }
        public string? ContractNumber { get; set; }
        public DateTime? ContractSignDate { get; set; }
        public string? ContractJobTitle { get; set; }
    }
}
