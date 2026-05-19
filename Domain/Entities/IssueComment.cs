namespace Domain.Entities
{
    public class IssueComment
    {
        public Guid Id { get; set; }
        public Guid IssueId { get; set; }
        public string Content { get; set; } = null!;
        public Guid? AuthorAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Issue Issue { get; set; } = null!;
    }
}
