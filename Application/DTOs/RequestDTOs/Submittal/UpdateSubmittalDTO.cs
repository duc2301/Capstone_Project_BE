using System.ComponentModel.DataAnnotations;
using Domain.Enum.Submittal;

namespace Application.DTOs.RequestDTOs.Submittal
{
    public class UpdateSubmittalDTO
    {
        [StringLength(250)]
        public string? Title { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        public SubmittalStatus? Status { get; set; }
        public SubmittalWorkflowType? WorkflowType { get; set; }
        public Guid? ContractPackageId { get; set; }
        public Guid? SubmittedByOrganizationId { get; set; }
    }
}
