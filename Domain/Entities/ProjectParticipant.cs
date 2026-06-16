using Domain.Common;
using Domain.Enum.Project;

namespace Domain.Entities
{
    // 1 group = 1 bên tham gia 1 dự án. Org/phòng ban suy ra qua Group.OrganizationId.
    public class ProjectParticipant : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid GroupId { get; set; }
        public ProjectParticipantRole Role { get; set; }
        public ProjectParticipantStatus Status { get; set; }   // Active / Inactive (xóa mềm khỏi dự án)
        public DateTime? JoinedAt { get; set; }

        public Project Project { get; set; } = null!;
        public Group Group { get; set; } = null!;
    }
}
