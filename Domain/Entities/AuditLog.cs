using Domain.Enum.Audit;

namespace Domain.Entities
{
    // Nhật ký hoạt động (chỉ admin dự án xem). Ghi nhận lịch sử thao tác.
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? ActorAccountId { get; set; }
        public AuditAction Action { get; set; }
        public string EntityType { get; set; } = null!;
        public string EntityId { get; set; } = null!;
        public string? DetailJson { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
