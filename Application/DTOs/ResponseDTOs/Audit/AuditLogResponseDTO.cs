using Domain.Enum.Audit;

namespace Application.DTOs.ResponseDTOs.Audit
{
    // 1 dòng nhật ký trả về cho FE. ActorName lấy bằng join Accounts (entity không snapshot tên).
    public class AuditLogResponseDTO
    {
        public Guid Id { get; set; }
        public LogScope Scope { get; set; }
        public AuditAction Action { get; set; }

        public Guid? ActorAccountId { get; set; }
        public string? ActorName { get; set; }

        public Guid? ProjectId { get; set; }
        public Guid? FolderId { get; set; }
        public Guid? GroupId { get; set; }

        public string EntityType { get; set; } = null!;
        public string EntityId { get; set; } = null!;

        public string? Detail { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
