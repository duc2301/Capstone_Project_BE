using Domain.Enum.Issue;

using Domain.Common;

namespace Domain.Entities
{
    // Issue / RFI: vấn đề hoặc yêu cầu thông tin giữa các bên
    public class Issue : IEntity, IAuditable
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public IssueType Type { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public IssueStatus Status { get; set; }
        public IssuePriority Priority { get; set; }
        public Guid? RaisedByAccountId { get; set; }
        public Guid? AssignedToAccountId { get; set; }
        public Guid? AssignedToOrganizationId { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? LinkedFolderId { get; set; }
        public Guid? LinkedFileItemId { get; set; }
        public string? ModelLocationJson { get; set; }   // vị trí trên mô hình 3D
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<IssueComment> Comments { get; set; } = new List<IssueComment>();
        public ICollection<IssueAttachment> Attachments { get; set; } = new List<IssueAttachment>();
    }
}
