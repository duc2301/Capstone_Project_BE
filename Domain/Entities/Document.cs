using Domain.Common;
using Domain.Enum.Cde;
using Domain.Enum.Rag;

namespace Domain.Entities
{
    public class Document : IEntity
    {
        public Guid Id { get; set; }

        // --- Nguồn gốc trong CDE (tham chiếu bằng Id, không nav) ---
        public Guid SourceFileVersionId { get; set; } 
        public Guid FileItemId { get; set; }            
        public Guid ProjectId { get; set; }             
        public CdeArea Area { get; set; }               
        public string? Discipline { get; set; }

        // --- Hiển thị / trích dẫn ---
        public string FileName { get; set; } = null!;
        public string? Format { get; set; }
        public string? Revision { get; set; }
        public DateTime UpdateAt { get; set; } = DateTime.UtcNow;

        // --- Vận hành pipeline ingest ---
        public string? ContentHash { get; set; }                                 
        public DocumentIngestStatus Status { get; set; } = DocumentIngestStatus.Pending;
        public DateTime? IngestedAt { get; set; }
        public int ChunkCount { get; set; }

        public ICollection<DocumentParentChunk> Chunks { get; set; } = new List<DocumentParentChunk>();
    }
}
