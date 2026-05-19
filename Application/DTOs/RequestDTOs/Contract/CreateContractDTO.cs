using System.ComponentModel.DataAnnotations;
using Domain.Enum.Contract;

namespace Application.DTOs.RequestDTOs.Contract
{
    public class CreateContractDTO
    {
        [Required]
        public Guid ContractPackageId { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = null!;

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        public Guid? ContractorOrganizationId { get; set; }
        public Guid? SourceFileVersionId { get; set; }
        public DateTime? SignedDate { get; set; }

        [Required]
        public ContractStatus Status { get; set; }
    }
}
