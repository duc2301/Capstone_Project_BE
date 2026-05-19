using Domain.Common;

namespace Domain.Entities
{
    // Nhóm thành viên dùng lại được, có thể gán vào nhiều dự án
    public class Group : IEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid? OrganizationId { get; set; }   // nhóm có thể thuộc 1 đối tác
        public DateTime? CreatedAt { get; set; }

        public Organization? Organization { get; set; }
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
}
