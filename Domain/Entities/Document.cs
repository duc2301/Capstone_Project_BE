using Domain.Common;
using Domain.Enum.Cde;
using Domain.Enum.Rag;

namespace Domain.Entities
{
    // 1 tài liệu CDE đã được nạp cho RAG = bản chụp nội dung của 1 FileVersion đã Published.
    // Module RAG tách bạch: chỉ tham chiếu CDE qua Id (KHÔNG navigation vào FileItem/FileVersion/Project),
    // ranh giới bảo mật gói trong ProjectId. Nội dung thật nằm ở các DocumentChunk.
    public class Document : IEntity
    {
        public Guid Id { get; set; }

        // --- Nguồn gốc trong CDE (tham chiếu bằng Id, không nav) ---
        public Guid SourceFileVersionId { get; set; }   // bản version chính xác đã embed (immutable)
        public Guid FileItemId { get; set; }            // file gốc — gom nhóm + hiển thị
        public Guid ProjectId { get; set; }             // ranh giới bảo mật RAG (denormalized)
        public CdeArea Area { get; set; }               // chỉ Published mới được nạp

        // --- Hiển thị / trích dẫn ---
        public string FileName { get; set; } = null!;
        public string? Format { get; set; }

        // --- Vận hành pipeline ingest ---
        public string? ContentHash { get; set; }                                  // bỏ qua re-embed nếu nội dung trùng
        public DocumentIngestStatus Status { get; set; } = DocumentIngestStatus.Pending;
        public DateTime? IngestedAt { get; set; }
        public int ChunkCount { get; set; }

        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}
