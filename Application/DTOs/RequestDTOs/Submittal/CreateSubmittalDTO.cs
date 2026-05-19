using System.ComponentModel.DataAnnotations;
using Domain.Enum.Submittal;

namespace Application.DTOs.RequestDTOs.Submittal
{
    public class CreateSubmittalDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        public Guid? ContractPackageId { get; set; }
        public Guid? ParentSubmittalId { get; set; }

        [Required]
        [StringLength(250)]
        public string Title { get; set; } = null!;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        public SubmittalStatus Status { get; set; }

        [Required]
        public SubmittalWorkflowType WorkflowType { get; set; }

        public Guid? SubmittedByOrganizationId { get; set; }
    }
}
