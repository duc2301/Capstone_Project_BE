using Domain.Enum.Group;
using Domain.Enum.Invitation;

namespace Application.DTOs.ResponseDTOs.Invitation
{
    // Phiên bản "xem từ phía người được mời" — kèm tên Project/Group/Inviter để FE list khỏi gọi thêm.
    public class MyInvitationDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public Guid InvitedGroupId { get; set; }
        public string GroupName { get; set; } = null!;
        public GroupMemberRole Role { get; set; }
        public Guid? InvitedByAccountId { get; set; }
        public string? InvitedByName { get; set; }
        public InvitationStatus Status { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Note { get; set; }
    }
}
