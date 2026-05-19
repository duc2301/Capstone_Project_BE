using System.ComponentModel.DataAnnotations;
using Domain.Enum.ContractPackage;

namespace Application.DTOs.RequestDTOs.ContractPackage
{
    public class UpdateContractPackageDTO
    {
        [StringLength(50)]
        public string? Code { get; set; }

        [StringLength(250)]
        public string? Name { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        public decimal? ContractValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public PackageStatus? Status { get; set; }
        public bool? IsDefault { get; set; }
    }
}
