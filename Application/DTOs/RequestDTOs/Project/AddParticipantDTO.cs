using System.ComponentModel.DataAnnotations;
using Domain.Enum.Project;

namespace Application.DTOs.RequestDTOs.Project
{
    // Project Manager gọi: thêm bên tham gia (Organization và/hoặc Group) vô project.
    public class AddParticipantDTO
    {
        public Guid? OrganizationId { get; set; }
        public Guid? GroupId { get; set; }

        [Required]
        public ProjectParticipantRole Role { get; set; } = ProjectParticipantRole.Member;
    }
}
