using Domain.Enum.Submittal;

using Domain.Common;

namespace Domain.Entities
{
    // Phiếu yêu cầu: cấu trúc cây (phiếu gốc -> con -> cháu)
    public class Submittal : IEntity, IAuditable
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
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ContractPackage? ContractPackage { get; set; }
        public Submittal? ParentSubmittal { get; set; }
        public ICollection<Submittal> ChildSubmittals { get; set; } = new List<Submittal>();
        public ICollection<SubmittalStep> Steps { get; set; } = new List<SubmittalStep>();
        public ICollection<SubmittalAttachment> Attachments { get; set; } = new List<SubmittalAttachment>();
        public ICollection<SubmittalCitedFolder> CitedFolders { get; set; } = new List<SubmittalCitedFolder>();
    }
}
