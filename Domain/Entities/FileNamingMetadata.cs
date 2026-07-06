using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class FileNamingMetadata
    {
        public Guid Id { get; set; }
        public Guid? SelectedValueId { get; set; } 
        public Guid FileItemId { get; set; }
        public Guid NamingConventionFieldId { get; set; }
        public string Value { get; set; } = string.Empty; 
        public string? DisplayValue { get; set; } // For UI/audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public NamingConventionField Field { get; set; } = null!;
        public FileItem FileItem { get; set; } = null!;
        public NamingConventionFieldValue? SelectedValue { get; set; }
    }
}
