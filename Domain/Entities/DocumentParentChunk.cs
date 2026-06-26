using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DocumentParentChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid ProjectId { get; set; }
        public int ChunkIndex { get; set; }
        public string? Content { get; set; } 
        public string? SectionTitle { get; set; }
        public int? PageNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Document Document { get; set; } = null!;
        public ICollection<DocumentChildChunk> ChildChunks { get; set; } = new List<DocumentChildChunk>(); 
    }
}
