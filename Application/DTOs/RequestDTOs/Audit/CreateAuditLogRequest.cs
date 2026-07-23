using Domain.Enum.Audit;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.RequestDTOs.Audit
{
    public class CreateAuditLogRequest
    {
        public LogScope Scope { get; set; }
        public AuditAction Action { get; set; }
        public string EventType { get; set; } = null!;   // dùng hằng số AuditEvents.*

        // Ai làm (controller đọc claim rồi truyền xuống)
        public Guid? ActorAccountId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }

        // Bối cảnh / scope keys
        public Guid? ProjectId { get; set; }
        public Guid? FolderId { get; set; }
        public Guid? GroupId { get; set; }

        // Đối tượng bị tác động
        public string EntityType { get; set; } = null!;  // nameof(FileItem)...
        public string EntityId { get; set; } = null!;
        public string? EntityName { get; set; }

        // Chi tiết
        public string? Summary { get; set; }
        public object? Detail { get; set; }   // sẽ serialize sang DetailJson (text)
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}
