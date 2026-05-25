using System.ComponentModel.DataAnnotations;
using Domain.Enum.Discussion;

namespace Application.DTOs.RequestDTOs.Discussion
{
    public class CreateDiscussionDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [StringLength(250)]
        public string Title { get; set; } = null!;

        [Required]
        public DiscussionScopeType ScopeType { get; set; }

        public Guid? ScopeId { get; set; }

        [Required]
        public DiscussionStatus Status { get; set; }
    }
}
