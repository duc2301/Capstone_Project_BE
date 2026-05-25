namespace Domain.Entities
{
    // Tin nhắn trong thảo luận. IsSolution = tích xanh "Giải pháp".
    // RecalledAt: không cho thu hồi sau 1 giờ (rule xử lý ở tầng service).
    public class DiscussionMessage
    {
        public Guid Id { get; set; }
        public Guid DiscussionId { get; set; }
        public string Content { get; set; } = null!;
        public Guid AuthorAccountId { get; set; }
        public bool IsSolution { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? RecalledAt { get; set; }

        public Discussion Discussion { get; set; } = null!;
        public DiscussionMessage? ReplyToMessage { get; set; }
        public ICollection<DiscussionMessage> Replies { get; set; } = new List<DiscussionMessage>();
        public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
        public ICollection<MessageMention> Mentions { get; set; } = new List<MessageMention>();
    }
}
