namespace Domain.Entities
{
    // @mention trong tin nhắn thảo luận
    public class MessageMention
    {
        public Guid Id { get; set; }
        public Guid DiscussionMessageId { get; set; }
        public Guid MentionedAccountId { get; set; }

        public DiscussionMessage DiscussionMessage { get; set; } = null!;
    }
}
