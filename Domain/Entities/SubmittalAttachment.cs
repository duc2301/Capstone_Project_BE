namespace Domain.Entities
{
    public class SubmittalAttachment
    {
        public Guid Id { get; set; }
        public Guid SubmittalId { get; set; }
        public Guid FileVersionId { get; set; }
        public Guid? AttachedByAccountId { get; set; }
        public DateTime? AttachedAt { get; set; }

        public Submittal Submittal { get; set; } = null!;
        public FileVersion FileVersion { get; set; } = null!;
    }
}
