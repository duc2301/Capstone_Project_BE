namespace Domain.Entities
{
    // Mảnh tài liệu cho RAG. Embedding ánh xạ sang cột pgvector `vector`.
    // Khi cài package Pgvector.EntityFrameworkCore, đổi float[] -> Pgvector.Vector.
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public string DocumentId { get; set; } = null!;   // FK tới Document.Id (string)
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = null!;
        public float[]? Embedding { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Document Document { get; set; } = null!;
    }
}
