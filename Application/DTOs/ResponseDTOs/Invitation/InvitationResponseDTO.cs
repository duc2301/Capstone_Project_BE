using Domain.Enum.Invitation;

namespace Application.DTOs.ResponseDTOs.Invitation
{
    public class InvitationResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? InvitedAccountId { get; set; }
        public Guid? InvitedGroupId { get; set; }
        public Guid? InvitedByAccountId { get; set; }
        public string Token { get; set; } = null!;
        public InvitationStatus Status { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string? Note { get; set; }
    }
}
