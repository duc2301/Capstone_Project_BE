using Domain.Common;
using Domain.Enum.Audit;

namespace Domain.Entities
{
    // Nhật ký hoạt động (chỉ admin dự án xem). Ghi nhận lịch sử thao tác.
    public class AuditLog
    {
        public Guid Id { get; set; }
        public LogScope Scope { get; set; }
        public AuditAction Action { get; set; }          // Create/Update/Approve/Upload/Sign...
        public Guid? ActorAccountId { get; set; }        // ai làm

        // bối cảnh
        public Guid? ProjectId { get; set; }
        public Guid? FolderId { get; set; }
        public Guid? GroupId { get; set; }

        // đối tượng bị tác động
        public string EntityType { get; set; } = null!;
        public string EntityId { get; set; } = null!;

        public string? Detail { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
