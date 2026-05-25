using Domain.Enum.Discussion;

namespace Application.DTOs.ResponseDTOs.Discussion
{
    public class DiscussionResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = null!;
        public DiscussionScopeType ScopeType { get; set; }
        public Guid? ScopeId { get; set; }
        public DiscussionStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
