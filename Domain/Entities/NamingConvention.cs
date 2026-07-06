using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class NamingConvention
    {
        public Guid Id { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid CreatedById { get; set; }
        public Guid FolderId { get; set; }
        public Folder Folder { get; set; } = null!;
        public string Delimiter { get; set; } = "-"; // e.g., -, _, .
        public bool IsActive { get; set; } = true;
        public ICollection<NamingConventionField> Fields { get; set; } = new List<NamingConventionField>();
    }
}
