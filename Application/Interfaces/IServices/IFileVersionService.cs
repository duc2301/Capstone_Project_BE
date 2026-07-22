using Application.DTOs.RequestDTOs.FileVersion;
using Application.DTOs.ResponseDTOs.FileVersion;

namespace Application.Interfaces.IServices
{
    // File Versioning: mọi quy tắc tính version (P{Rev}.{Ver} / C{PubRev}) nằm duy nhất ở service này.
    // KHÔNG upload file, KHÔNG chuyển zone, KHÔNG check quyền — caller tự lo các việc đó rồi gọi vào đây.
    // Dữ liệu file vật lý do caller truyền vào (FileVersionDataDTO) — không đọc từ hệ FileVersions cũ.
    public interface IFileVersionService
    {
        // Upload vào folder: tài liệu mới -> trả P01.01 (chưa lưu, chờ caller tạo FileItem);
        // tài liệu đã tồn tại (trùng Name) -> Working Version +1, lưu dòng state mới kèm dữ liệu file.
        Task<FileVersionResult> GetNextUploadVersionAsync(Guid folderId, string fileName, FileVersionDataDTO? fileData = null);

        // Chốt version đầu tiên (P01.01) cho FileItem vừa được tạo, kèm dữ liệu file vật lý.
        Task<FileVersionResult> CreateInitialVersionAsync(Guid fileItemId, FileVersionDataDTO? fileData = null);

        // Tài liệu vào SHARED thành công: Working Revision +1, Working Version reset về 01.
        // Dữ liệu file giữ nguyên (copy từ dòng state trước).
        Task<FileVersionResult> GetNextSharedVersionAsync(Guid fileItemId);

        // Publish: Published Revision +1, hiển thị C{PubRev} (không có Version Number).
        Task<FileVersionResult> GetNextPublishedVersionAsync(Guid fileItemId);

        // Quay về WIP từ Published: giữ Working Revision, Working Version reset về 01,
        // Published Revision được bảo toàn nội bộ.
        Task<FileVersionResult> GetReturnToWipVersionAsync(Guid fileItemId);

        // Khôi phục 1 version cũ làm version hiện hành: tạo dòng state MỚI copy dữ liệu file của version
        // được chọn, đánh số theo đúng luật "upload thay thế" (WorkingVersion +1) và cập nhật
        // FileItem.CurrentVersionId. Tài liệu đang Published phải về WIP trước.
        Task<FileVersionResult> RestoreVersionAsync(Guid fileItemId, Guid versionStateId);

        // Trạng thái version hiện hành (null nếu tài liệu chưa có state).
        Task<FileVersionResult?> GetCurrentVersionAsync(Guid fileItemId);

        // Toàn bộ lịch sử version (mới nhất trước), kèm snapshot dữ liệu file của từng version.
        Task<List<FileVersionHistoryItemDTO>> GetVersionHistoryAsync(Guid fileItemId);
    }
}
