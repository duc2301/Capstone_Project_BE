using System.ComponentModel.DataAnnotations;
using Domain.Enum.ContractPackage;

namespace Application.DTOs.RequestDTOs.ContractPackage
{
    public class CreateContractPackageDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = null!;

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
    }
}
