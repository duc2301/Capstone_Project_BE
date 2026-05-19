using Domain.Enum.Issue;

namespace Application.DTOs.ResponseDTOs.Issue
{
    public class IssueResponseDTO : IResponseDto
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
        public string? ModelLocationJson { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
