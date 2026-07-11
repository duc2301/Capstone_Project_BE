using Application.DTOs.ResponseDTOs.Common;
using Domain.Enum.Discussion;

namespace Application.DTOs.ResponseDTOs.Discussion
{
    public class DiscussionMessageResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid DiscussionId { get; set; }
        public string Content { get; set; } = null!;
        public Guid AuthorAccountId { get; set; }
        public string? AuthorName { get; set; }
        public bool IsSolution { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<MessageAttachmentResponseDTO> Attachments { get; set; } = new();
        public List<AccountRefDTO> Mentions { get; set; } = new();
    }

    public class MessageAttachmentResponseDTO
    {
        public Guid Id { get; set; }
        public MessageAttachmentType Type { get; set; }
        public Guid? FileVersionId { get; set; }
        public string? Url { get; set; }
        public Guid? FolderId { get; set; }
    }
}
