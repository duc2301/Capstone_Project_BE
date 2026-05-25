using System.ComponentModel.DataAnnotations;
using Domain.Enum.Contract;

namespace Application.DTOs.RequestDTOs.Contract
{
    public class UpdateContractDTO
    {
        [StringLength(50)]
        public string? Code { get; set; }

        [StringLength(250)]
        public string? Name { get; set; }

        public Guid? ContractorOrganizationId { get; set; }
        public Guid? SourceFileVersionId { get; set; }
        public DateTime? SignedDate { get; set; }
        public ContractStatus? Status { get; set; }
    }
}
