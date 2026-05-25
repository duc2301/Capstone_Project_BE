using Domain.Enum.Project;

namespace Domain.Entities
{
    // Gán Tổ chức/Nhóm vào 1 dự án kèm vai trò dự án
    public class ProjectParticipant
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? GroupId { get; set; }
        public ProjectParticipantRole Role { get; set; }
        public DateTime? JoinedAt { get; set; }

        public Project Project { get; set; } = null!;
        public Organization? Organization { get; set; }
        public Group? Group { get; set; }
    }
}
