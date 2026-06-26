using Domain.Common;
using Pgvector;

namespace Domain.Entities
{
    public class DocumentChildChunk : IEntity
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }      // FK -> Document (cha)
        public Guid ProjectId { get; set; }       // denormalized — ranh giới bảo mật khi truy vấn

        public int ChunkIndex { get; set; }
        public string Content { get; set; } = null!;
        public Vector? Embedding { get; set; }    // null cho tới khi embed xong
        public DateTime? CreatedAt { get; set; }
        public Guid ParentChunkId { get; set; }
        public DocumentParentChunk ParentChunk { get; set; } = null!;
    }
}
