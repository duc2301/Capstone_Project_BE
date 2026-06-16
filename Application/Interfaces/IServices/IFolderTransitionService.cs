using Application.DTOs.ResponseDTOs.Folder;
using Domain.Enum.Cde;

namespace Application.Interfaces.IServices
{
    // Chuyển trạng thái CDE theo ISO 19650 — tiến đúng 1 bậc Wip→Shared→Published→Archived.
    //  - Wip→Shared, Shared→Published: COPY (giữ bản gốc, mirror cấu trúc, copy blob, lưu vết nguồn).
    //  - Published→Archived: MOVE (thu hồi khỏi Published vào Archived).
    // Cổng quyền: Shared cần CanUpdate, Published/Archived cần CanApprove (trên thư mục nguồn).
    public interface IFolderTransitionService
    {
        // Cả thư mục (đệ quy): mirror cây con sang khu vực đích, copy/move file bên trong.
        Task<TransitionResultDTO> PromoteFolderAsync(Guid folderId, CdeArea targetArea);

        // 1 tài liệu, có thể chọn version (mặc định version hiện hành).
        Task<TransitionResultDTO> PromoteFileAsync(Guid fileItemId, CdeArea targetArea, Guid? versionId);
    }
}
