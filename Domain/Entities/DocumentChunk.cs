using Domain.Common;
using Pgvector;

namespace Domain.Entities
{
    // Mảnh nội dung của 1 Document cho RAG.
    // Embedding = cột pgvector (vector(N), N cấu hình trong DbContext).
    // ProjectId denormalize từ Document để lọc quyền NGAY trong câu vector search
    // (WHERE ProjectId = @p) mà không cần join — nhanh + đơn giản.
    public class DocumentChunk : IEntity
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }      // FK -> Document (cha)
        public Guid ProjectId { get; set; }       // denormalized — ranh giới bảo mật khi truy vấn

        public int ChunkIndex { get; set; }
        public string Content { get; set; } = null!;
        public Vector? Embedding { get; set; }    // null cho tới khi embed xong
        public DateTime? CreatedAt { get; set; }

        public Document Document { get; set; } = null!;
    }
}
