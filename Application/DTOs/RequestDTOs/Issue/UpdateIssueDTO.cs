using System.ComponentModel.DataAnnotations;
using Domain.Enum.Issue;

namespace Application.DTOs.RequestDTOs.Issue
{
    public class UpdateIssueDTO
    {
        public IssueType? Type { get; set; }

        [StringLength(250)]
        public string? Title { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        public IssueStatus? Status { get; set; }
        public IssuePriority? Priority { get; set; }
        public Guid? AssignedToAccountId { get; set; }
        public Guid? AssignedToOrganizationId { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? LinkedFolderId { get; set; }
        public Guid? LinkedFileItemId { get; set; }
        public string? ModelLocationJson { get; set; }
    }
}
