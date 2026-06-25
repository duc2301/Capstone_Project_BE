using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class NamingConventionFieldValue
    {
        public Guid Id { get; set; }
        public Guid CreatedById { get; set; }
        public Guid NamingConventionFieldId { get; set; }
        public string Code { get; set; } = string.Empty; // ARC, STR, MEC
        public string DisplayName { get; set; } = string.Empty; // Architecture, Structural, Mechanical
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsLocked { get; set; }
        public bool IsActive { get; set; } = true;
        public NamingConventionField Field { get; set; } = null!;
        public NamingConventionLockedValue? LockedValue { get; set; }

    }
}
