namespace Domain.Entities
{
    // Folder trích dẫn trong thảo luận -> nguồn quyền của thảo luận
    public class DiscussionCitedFolder
    {
        public Guid Id { get; set; }
        public Guid DiscussionId { get; set; }
        public Guid FolderId { get; set; }

        public Discussion Discussion { get; set; } = null!;
        public Folder Folder { get; set; } = null!;
    }
}
