using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Invitation
{
    // Mời 1 account cụ thể vào 1 group cụ thể (group đó đã/sẽ tham gia project).
    // Group muốn add cả nhóm vô project -> dùng /api/projects/{id}/participants/bulk thay vì invite.
    public class InviteRequestDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public Guid InvitedAccountId { get; set; }

        [Required]
        public Guid InvitedGroupId { get; set; }

        public int ExpireDays { get; set; } = 7;

        [StringLength(500)]
        public string? Note { get; set; }

        // InvitedByAccountId lấy từ JWT (ICurrentUserService) — không cần body
    }
}
