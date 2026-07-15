using Application.DTOs.ResponseDTOs.FileVersion;

namespace Application.Interfaces.IServices
{
    // File Versioning: mọi quy tắc tính version (P{Rev}.{Ver} / C{PubRev}) nằm duy nhất ở service này.
    // KHÔNG upload file, KHÔNG chuyển zone, KHÔNG check quyền — caller tự lo các việc đó rồi gọi vào đây.
    public interface IFileVersionService
    {
        // Upload vào folder: tài liệu mới -> trả P01.01 (chưa lưu state, chờ caller tạo FileItem);
        // tài liệu đã tồn tại (trùng Name) -> Working Version +1 và lưu state.
        Task<FileVersionResult> GetNextUploadVersionAsync(Guid folderId, string fileName);

        // Chốt version đầu tiên (P01.01) cho FileItem vừa được tạo.
        Task<FileVersionResult> CreateInitialVersionAsync(Guid fileItemId);

        // Tài liệu vào SHARED thành công: Working Revision +1, Working Version reset về 01.
        Task<FileVersionResult> GetNextSharedVersionAsync(Guid fileItemId);

        // Publish: Published Revision +1, hiển thị C{PubRev} (không có Version Number).
        Task<FileVersionResult> GetNextPublishedVersionAsync(Guid fileItemId);

        // Quay về WIP từ Published: giữ Working Revision, Working Version reset về 01,
        // Published Revision được bảo toàn nội bộ.
        Task<FileVersionResult> GetReturnToWipVersionAsync(Guid fileItemId);

        // Trạng thái version hiện hành (null nếu tài liệu chưa có state).
        Task<FileVersionResult?> GetCurrentVersionAsync(Guid fileItemId);

        // Toàn bộ lịch sử version (mới nhất trước), kèm snapshot dữ liệu file của từng version.
        Task<List<FileVersionHistoryItemDTO>> GetVersionHistoryAsync(Guid fileItemId);
    }
}
