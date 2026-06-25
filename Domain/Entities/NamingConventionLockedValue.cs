using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class NamingConventionLockedValue
    {
        public Guid Id { get; set; }
        public Guid? CreatedById { get; set; }
        public Guid NamingConventionFieldId { get; set; }
        public Guid NamingConventionFieldValueId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public NamingConventionField Field { get; set; } = null!;
        public NamingConventionFieldValue Value { get; set; } = null!;
    }
}