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

        // RaisedByAccountId KHONG duoc client set - lay tu actor da xac thuc (User.GetAccountId()) o service,
        // tranh truong hop client tu xung la nguoi khac tao issue.
        public Guid? AssignedToAccountId { get; set; }
        public Guid? AssignedToOrganizationId { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? LinkedFolderId { get; set; }
        public Guid? LinkedFileItemId { get; set; }
        public string? ModelLocationJson { get; set; }
    }
}
