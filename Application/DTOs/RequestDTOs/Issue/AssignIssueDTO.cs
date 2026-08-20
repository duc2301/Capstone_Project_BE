using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Issue
{
    public class AssignIssueDTO
    {
        public Guid? AssignedToAccountId { get; set; }
        public Guid? AssignedToGroupId { get; set; }
    }

    public class RejectIssueAssignmentDTO
    {
        [Required]
        [StringLength(1000, MinimumLength = 5)]
        public string Reason { get; set; } = null!;
    }
}
