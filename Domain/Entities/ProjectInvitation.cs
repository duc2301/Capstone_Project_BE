using Domain.Common;
using Domain.Enum.Group;
using Domain.Enum.Invitation;

namespace Domain.Entities
{
    // Lời mời 1 account tham gia 1 group thuộc 1 project (do Project Manager phát).
    // Accept (by Id, có JWT) -> service tạo GroupMember(Role) + auto-link ProjectParticipant nếu cần.
    // Token vẫn giữ trong DB cho email cold-link sau này, không expose qua response/route in-app.
    public class ProjectInvitation : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? InvitedAccountId { get; set; }   // account được mời
        public Guid? InvitedGroupId { get; set; }     // group đích
        public GroupMemberRole Role { get; set; }     // member/leader của group đó
        public Guid? InvitedByAccountId { get; set; } // ai phát lời mời (PM)
        public string Token { get; set; } = null!;    // dự phòng cho email cold-link
        public InvitationStatus Status { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string? Note { get; set; }
    }
}
