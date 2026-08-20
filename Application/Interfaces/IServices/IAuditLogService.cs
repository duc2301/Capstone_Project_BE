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

        // Ghi + commit, NHƯNG bỏ qua nếu cùng actor đã ghi cùng hành động trên cùng đối tượng
        // trong cửa sổ thời gian gần đây. Dành cho hành động lặp lại nhiều lần (mở xem tài liệu):
        // không có bước này thì mở đi mở lại một tệp sẽ đẻ ra hàng chục dòng log rác.
        Task LogThrottledAsync(
            LogScope scope,
            AuditAction action,
            string entityType,
            string entityId,
            Guid actorId,
            string? detail = null,
            Guid? projectId = null,
            Guid? folderId = null,
            Guid? groupId = null,
            TimeSpan? window = null
        );

        // ===== ĐỌC =====

        // Admin hệ thống: nhật ký mọi dự án (có thể lọc theo 1 dự án qua filter.ProjectId).
        Task<AuditLogPageDTO> GetSystemAsync(AuditLogFilterDTO filter, Guid actorId);

        // Admin hoặc PM của dự án: toàn bộ nhật ký trong dự án.
        Task<AuditLogPageDTO> GetByProjectAsync(
            Guid projectId, AuditLogFilterDTO filter, Guid actorId);

        // Thành viên: CHỈ nhật ký của folder mình được xem hoặc của nhóm mình.
        Task<AuditLogPageDTO> GetMyInProjectAsync(
            Guid projectId, AuditLogFilterDTO filter, Guid actorId);

        Task<AuditLogPageDTO> GetMyActivityAsync(AuditLogFilterDTO filter, Guid actorId);

        Task<AuditLogPageDTO> GetByFileItemAsync(Guid fileItemId, AuditLogFilterDTO filter, Guid actorId);

        Task<AuditLogExportDTO> ExportAsync(Guid? projectId, AuditLogFilterDTO filter, Guid actorId);
    }
}
