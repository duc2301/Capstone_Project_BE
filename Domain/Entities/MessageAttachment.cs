using Domain.Enum.Discussion;

namespace Domain.Entities
{
    public class MessageAttachment
    {
        public Guid Id { get; set; }
        public Guid DiscussionMessageId { get; set; }
        public MessageAttachmentType Type { get; set; }
        public Guid? FileVersionId { get; set; }
        public string? Url { get; set; }            // dùng cho Image ngoài / Link nhúng
        public Guid? FolderId { get; set; }         // dùng cho CitedFolder

        public DiscussionMessage DiscussionMessage { get; set; } = null!;
    }
}
