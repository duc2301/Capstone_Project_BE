using System.ComponentModel.DataAnnotations;
using Domain.Enum.Issue;

namespace Application.DTOs.RequestDTOs.Issue
{
    public class CreateIssueDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public IssueType Type { get; set; }

        [Required]
        [StringLength(250)]
        public string Title { get; set; } = null!;

        [StringLength(4000)]
        public string? Description { get; set; }

        [Required]
        public IssueStatus Status { get; set; }

        [Required]
        public IssuePriority Priority { get; set; }

        public Guid? RaisedByAccountId { get; set; }
        public Guid? AssignedToAccountId { get; set; }
        public Guid? AssignedToOrganizationId { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? LinkedFolderId { get; set; }
        public Guid? LinkedFileItemId { get; set; }
        public string? ModelLocationJson { get; set; }
    }
}
