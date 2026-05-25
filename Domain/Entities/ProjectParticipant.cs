using Domain.Enum.Project;

namespace Domain.Entities
{
    // Gán Tổ chức / Nhóm / Phòng ban vô 1 dự án — 1 dòng = 1 bên tham gia.
    // Đúng 1 trong 3 (DepartmentId / OrganizationId / GroupId) phải có giá trị.
    public class ProjectParticipant
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? DepartmentId { get; set; }       // phòng ban tham gia
        public ProjectParticipantRole Role { get; set; }
        public DateTime? JoinedAt { get; set; }

        public Project Project { get; set; } = null!;
        public Organization? Organization { get; set; }
        public Group? Group { get; set; }
        public Department? Department { get; set; }
    }
}
