using Application.DTOs.RequestDTOs.Audit;
using Application.DTOs.ResponseDTOs.Audit;
using Domain.Enum.Audit;

namespace Application.Interfaces.IServices
{
    public interface IAuditLogService
    {
        // ===== GHI =====

        // Add bản ghi vào UnitOfWork hiện tại, KHÔNG tự commit.
        // Gọi ngay TRƯỚC CommitAsync() của nghiệp vụ -> log và nghiệp vụ atomic.
        Task LogAsync(
            LogScope scope,
            AuditAction action,
            string entityType,
            string entityId,
            Guid? actorId,
            string? detail = null,
            Guid? projectId = null,
            Guid? folderId = null,
            Guid? groupId = null
        );

        // Ghi + commit ngay. Dùng cho luồng chỉ-đọc (Download/View) vốn không có transaction sẵn.
        Task LogAndSaveAsync(
            LogScope scope,
            AuditAction action,
            string entityType,
            string entityId,
            Guid? actorId,
            string? detail = null,
            Guid? projectId = null,
            Guid? folderId = null,
            Guid? groupId = null
        );

        // ===== ĐỌC =====

        // Admin hệ thống: nhật ký mọi dự án (có thể lọc theo 1 dự án qua filter.ProjectId).
        Task<AuditLogPageDTO> GetSystemAsync(AuditLogFilterDTO filter, bool isSystemAdmin);

        // Admin hoặc PM của dự án: toàn bộ nhật ký trong dự án.
        Task<AuditLogPageDTO> GetByProjectAsync(
            Guid projectId, AuditLogFilterDTO filter, Guid actorId, bool isSystemAdmin);

        // Thành viên: CHỈ nhật ký của folder mình được xem hoặc của nhóm mình.
        Task<AuditLogPageDTO> GetMyInProjectAsync(
            Guid projectId, AuditLogFilterDTO filter, Guid actorId);
    }
}
