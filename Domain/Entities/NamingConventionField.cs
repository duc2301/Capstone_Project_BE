using Domain.Enum.FileNaming;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class NamingConventionField
    {
        public Guid Id { get; set; }
        public Guid CreatedById { get; set; }
        public Guid NamingConventionId { get; set; }
        public string Code { get; set; } = string.Empty; // e.g., "Project", "Originator"
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public bool IsRequired { get; set; } // meaning this field must always be filled, the data can be anything, but it cannot be empty
        public bool IsLocked { get; set; } // meaning this field and its only value is locked and subsequent folders are not allowed to change it
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public NamingFieldType FieldType { get; set; } = NamingFieldType.Custom;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public NamingConvention NamingConvention { get; set; } = null!;
        public ICollection<NamingConventionFieldValue> AllowedValues { get; set; } = new List<NamingConventionFieldValue>();
        public NamingConventionLockedValue? LockedValue { get; set; }
    }
}
