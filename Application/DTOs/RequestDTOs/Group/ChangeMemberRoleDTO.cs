using System.ComponentModel.DataAnnotations;
using Domain.Enum.Group;

namespace Application.DTOs.RequestDTOs.Group
{
    // Đổi vai trò 1 thành viên Active trong nhóm.
    // Đặt Role = Leader = chuyển trưởng nhóm (Leader hiện tại tự động bị hạ xuống Member).
    public class ChangeMemberRoleDTO
    {
        [Required]
        public GroupMemberRole Role { get; set; }
    }
}
