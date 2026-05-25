namespace Domain.Entities
{
    public class GroupMember
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid AccountId { get; set; }
        public DateTime? JoinedAt { get; set; }

        public Group Group { get; set; } = null!;
        public Account Account { get; set; } = null!;
    }
}
