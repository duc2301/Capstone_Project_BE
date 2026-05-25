using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Invitation
{
    public class InviteRequestDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        // Mời 1 account cụ thể HOẶC 1 nhóm — phải có ít nhất 1
        public Guid? InvitedAccountId { get; set; }
        public Guid? InvitedGroupId { get; set; }

        public int ExpireDays { get; set; } = 7;

        [StringLength(500)]
        public string? Note { get; set; }

        // InvitedByAccountId lấy từ JWT (ICurrentUserService) — không cần body
    }
}
