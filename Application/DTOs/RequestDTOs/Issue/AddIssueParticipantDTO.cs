using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Issue
{
    public class AddIssueParticipantDTO
    {
        [Required]
        public Guid AccountId { get; set; }
    }
}
