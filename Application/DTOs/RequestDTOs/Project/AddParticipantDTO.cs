using System.ComponentModel.DataAnnotations;
using Domain.Enum.Project;

namespace Application.DTOs.RequestDTOs.Project
{
    // 1 bên tham gia = 1 Group (thông tin tổ chức xem qua Group.OrganizationId)
    public class AddParticipantDTO
    {
        [Required]
        public Guid GroupId { get; set; }

        [Required]
        public ProjectParticipantRole Role { get; set; } = ProjectParticipantRole.Member;
    }
}
