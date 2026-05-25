using Domain.Enum.Submittal;

namespace Application.DTOs.ResponseDTOs.Submittal
{
    public class SubmittalResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ContractPackageId { get; set; }
        public Guid? ParentSubmittalId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public SubmittalStatus Status { get; set; }
        public SubmittalWorkflowType WorkflowType { get; set; }
        public Guid? SubmittedByOrganizationId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
