using Application.DTOs.RequestDTOs.Audit;
using Application.DTOs.ResponseDTOs.Audit;

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

        // Các group đang Active mà account là thành viên Active trong 1 dự án.
        Task<HashSet<Guid>> GetMyActiveGroupIdsAsync(Guid projectId, Guid accountId);
    }
}
