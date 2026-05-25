using System.ComponentModel.DataAnnotations;
using Domain.Enum.Project;

namespace Application.DTOs.RequestDTOs.Project
{
    // 1 bên tham gia: đúng 1 trong 3 (DepartmentId / OrganizationId / GroupId) phải có.
    public class AddParticipantDTO
    {
        public Guid? DepartmentId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? GroupId { get; set; }

        [Required]
        public ProjectParticipantRole Role { get; set; } = ProjectParticipantRole.Member;
    }
}
