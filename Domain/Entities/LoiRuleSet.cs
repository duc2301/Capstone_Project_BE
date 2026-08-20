namespace Domain.Entities
{
    public class LoiRuleSet
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public bool IsSystem { get; set; }

        public Guid? CreatedByAccountId { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
