using Domain.Enum.Discussion;

using Domain.Common;

namespace Domain.Entities
{
    // Thảo luận: độc lập hoặc gắn vào File/Note/Submittal/Issue
    public class Discussion : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = null!;
        public DiscussionScopeType ScopeType { get; set; }
        public Guid? ScopeId { get; set; }            // id đối tượng được gắn (polymorphic)
        public DiscussionStatus Status { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<DiscussionMessage> Messages { get; set; } = new List<DiscussionMessage>();
        public ICollection<DiscussionCitedFolder> CitedFolders { get; set; } = new List<DiscussionCitedFolder>();
    }
}
