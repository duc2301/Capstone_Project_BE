using Application.DTOs.RequestDTOs.FileVersion;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileVersion;
using Domain.Entities;

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

        // CHỈ ĐỌC: tên tài liệu còn trống không, nếu bận thì tài liệu nào đang giữ và người dùng còn
        // lựa chọn nào (lên phiên bản / phải đổi tên). Không ném lỗi — luồng upload gọi để hỏi ý
        // người dùng TRƯỚC khi tải bytes lên, và để dò tên trống khi họ chọn tách tài liệu riêng.
        Task<NameAvailabilityDTO> CheckNameAvailabilityAsync(Guid folderId, string fileName, string? format);

        // CHỈ ĐỌC: nhãn version mà lần upload tới sẽ nhận, dùng để ĐẶT TÊN object cho dễ đọc.
        // Không ghi gì, không kiểm tra nghiệp vụ. Phải có vì luồng upload lưu bytes TRƯỚC khi chốt
        // version (GetNextUploadVersionAsync cần StoragePath trong fileData nên không đảo được thứ tự).
        // Hai upload cùng tên chạy song song có thể nhận cùng nhãn — vô hại: tên object vẫn duy nhất
        // nhờ hậu tố ngẫu nhiên, và số version thật luôn lấy từ DB chứ không đọc ngược từ key.
        Task<string> PeekNextUploadVersionAsync(Guid folderId, string fileName);

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
        Task<FileVersionResult> RestoreVersionAsync(Guid fileItemId, Guid versionStateId, Guid actorId);

        // Trạng thái version hiện hành (null nếu tài liệu chưa có state).
        Task<FileVersionResult?> GetCurrentVersionAsync(Guid fileItemId);

        // Toàn bộ lịch sử version (mới nhất trước), kèm snapshot dữ liệu file của từng version.
        Task<List<FileVersionHistoryItemDTO>> GetVersionHistoryAsync(Guid fileItemId);

        // Ký số xong: append bản đã ký (PDF đóng dấu chữ ký) theo luật "upload thay thế" (WorkingVersion +1).
        // KHÔNG phải upload: nội dung tài liệu không đổi, chỉ đổi định dạng + đóng dấu -> kết quả phân tích
        // AI (mô tả, cảnh báo) của bản được ký đi theo sang dòng state mới.
        Task<FileVersionResult> AppendSignedVersionAsync(Guid fileItemId, string fileName, FileVersionDataDTO signedData);

        // Niêm phong lưu trữ: append 1 dòng version cho FILE BẢN LƯU (trong Archived), copy nội dung +
        // số hiệu (C{PubRev}) từ bản Published gốc. Cộng dồn qua từng lần niêm phong (giữ cả C01, C02...).
        Task<FileVersionResult> AppendArchivedVersionAsync(Guid archivedFileItemId, FileVersionState sourcePublished);
    }
}
