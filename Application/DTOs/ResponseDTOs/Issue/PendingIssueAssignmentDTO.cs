namespace Application.DTOs.ResponseDTOs.Issue
{
    public class PendingIssueAssignmentDTO
    {
        public Guid IssueId { get; set; }
        public string Title { get; set; } = null!;
        public Guid ProjectId { get; set; }
        public Guid? LinkedFileItemId { get; set; }
        public string? AssignedToGroupName { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
