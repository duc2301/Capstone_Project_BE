namespace Domain.Entities
{
    public class IssueAttachment
    {
        public Guid Id { get; set; }
        public Guid IssueId { get; set; }
        public Guid? FileVersionId { get; set; }
        public string? Url { get; set; }

        public Issue Issue { get; set; } = null!;
    }
}
