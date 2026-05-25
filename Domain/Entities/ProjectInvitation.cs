using Domain.Common;
using Domain.Enum.Invitation;

namespace Domain.Entities
{
    // Lời mời 1 account/group tham gia 1 dự án (do Project Manager phát).
    // Người được mời accept bằng token -> service tạo ProjectParticipant.
    public class ProjectInvitation : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? InvitedAccountId { get; set; }   // mời theo account cụ thể
        public Guid? InvitedGroupId { get; set; }     // hoặc mời theo nhóm
        public Guid? InvitedByAccountId { get; set; } // ai phát lời mời (Manager)
        public string Token { get; set; } = null!;    // mã ngẫu nhiên gửi cho người được mời
        public InvitationStatus Status { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string? Note { get; set; }
    }
}
