using Application.DTOs.RequestDTOs.Audit;
using Application.DTOs.ResponseDTOs.Audit;
using Domain.Enum.Audit;

namespace Application.Interfaces.IRepositories
{
    // Truy vấn nhật ký hoạt động: luôn phân trang + lọc ngay trong DB
    // (KHÔNG dùng IGenericRepository.FindAsync vì bảng log phình rất nhanh).
    public interface IAuditLogRepository
    {
        // folderIds/groupIds = null  -> không giới hạn (view Admin / PM).
        // folderIds/groupIds != null -> chỉ lấy log thuộc folder user được xem HOẶC nhóm của user
        //                               (view thành viên — mặc định từ chối).
        Task<AuditLogPageDTO> QueryAsync(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<Guid>? folderIds,
            HashSet<Guid>? groupIds);

        Task<List<AuditLogResponseDTO>> QueryAllAsync(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<Guid>? folderIds,
            HashSet<Guid>? groupIds,
            int maxRows);

        Task<AuditLogPageDTO> QueryByEntitiesAsync(
            AuditLogFilterDTO filter,
            Guid? projectId,
            HashSet<string> entityTypes,
            HashSet<string> entityIds);

        // Các group đang Active mà account là thành viên Active trong 1 dự án.
        Task<HashSet<Guid>> GetMyActiveGroupIdsAsync(Guid projectId, Guid accountId);

        // Đã có dòng log cùng actor + cùng hành động + cùng đối tượng kể từ mốc since chưa.
        // Dùng để chặn ghi trùng cho hành động lặp lại nhiều lần (mở xem tài liệu).
        Task<bool> HasRecentAsync(
            AuditAction action, string entityType, string entityId, Guid actorId, DateTime since);
    }
}
