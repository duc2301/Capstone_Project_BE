using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    // Quy ước đặt tên file mức DỰ ÁN: 1 convention có thể gán cho nhiều folder (Folder.NamingConventionId).
    public class NamingConvention
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid CreatedById { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public string Name { get; set; } = string.Empty; // tên hiển thị trên trang cấu hình
        public string Delimiter { get; set; } = "-"; // e.g., -, _, .
        public bool IsActive { get; set; } = true;
        public ICollection<NamingConventionField> Fields { get; set; } = new List<NamingConventionField>();
        public ICollection<Folder> Folders { get; set; } = new List<Folder>();
    }
}
