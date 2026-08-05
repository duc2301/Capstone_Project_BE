using Domain.Enum.Audit;

namespace Application.DTOs.RequestDTOs.Audit
{
    // Bộ lọc chung cho 3 endpoint đọc nhật ký (bind từ query string).
    public class AuditLogFilterDTO
    {
        public LogScope? Scope { get; set; }
        public AuditAction? Action { get; set; }
        public Guid? ActorId { get; set; }
        public string? Search { get; set; }
        public Guid? ProjectId { get; set; }   // chỉ dùng ở view Admin (lọc theo dự án)
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
