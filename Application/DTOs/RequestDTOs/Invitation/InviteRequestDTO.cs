using System.ComponentModel.DataAnnotations;
using Domain.Enum.Group;

namespace Application.DTOs.RequestDTOs.Invitation
{
    // PM mời 1 account vào 1 group thuộc 1 project, với role Leader/Member.
    public class InviteRequestDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public Guid InvitedAccountId { get; set; }

        [Required]
        public Guid InvitedGroupId { get; set; }

        [Required]
        public GroupMemberRole Role { get; set; } = GroupMemberRole.Member;

        public int ExpireDays { get; set; } = 7;

        [StringLength(500)]
        public string? Note { get; set; }

        // InvitedByAccountId lấy từ JWT (controller truyền inviterId xuống service) — không cần body
    }
}
