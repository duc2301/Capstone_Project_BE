using Domain.Enum.Group;

namespace Domain.Entities
{
    public class GroupMember
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid AccountId { get; set; }
        public GroupMemberRole Role { get; set; }   // Leader hoặc Member trong group này
        public DateTime? JoinedAt { get; set; }

        public GroupMemberStatus Status { get; set; }

        public Group Group { get; set; } = null!;
        public Account Account { get; set; } = null!;

    }
}
