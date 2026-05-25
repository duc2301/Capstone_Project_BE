using System.ComponentModel.DataAnnotations;
using Domain.Enum.Discussion;

namespace Application.DTOs.RequestDTOs.Discussion
{
    public class UpdateDiscussionDTO
    {
        [StringLength(250)]
        public string? Title { get; set; }

        public DiscussionScopeType? ScopeType { get; set; }
        public Guid? ScopeId { get; set; }
        public DiscussionStatus? Status { get; set; }
    }
}
