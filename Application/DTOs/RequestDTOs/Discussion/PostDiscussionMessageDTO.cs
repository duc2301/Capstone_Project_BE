using System.ComponentModel.DataAnnotations;
using Domain.Enum.Discussion;

namespace Application.DTOs.RequestDTOs.Discussion
{
    public class PostDiscussionMessageDTO
    {
        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = null!;

        public Guid? ReplyToMessageId { get; set; }

        public List<PostMessageAttachmentDTO>? Attachments { get; set; }

        public List<Guid>? MentionedAccountIds { get; set; }
    }

    public class PostMessageAttachmentDTO
    {
        [Required]
        public MessageAttachmentType Type { get; set; }

        /// <summary>Bat buoc khi Type = File — id ban ghi FileVersion co san trong cay thu muc.</summary>
        public Guid? FileVersionId { get; set; }

        /// <summary>Bat buoc khi Type = Image/Link.</summary>
        public string? Url { get; set; }

        /// <summary>Bat buoc khi Type = CitedFolder.</summary>
        public Guid? FolderId { get; set; }
    }
}
