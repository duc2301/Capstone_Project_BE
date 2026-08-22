using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    // Luồng tải file lên (mức hệ thống hiện có — lưu đĩa local):
    //  ④ pre-upload consistency ⑤ kiểm tra version (trùng -> bản mới, đẩy bản cũ sang Archived)
    //  ⑦ lưu file vào đúng thư mục.
    public interface IFileUploadService
    {
        // actorId do controller lấy từ JWT truyền vào (gate quyền Edit/Update + lưu tác giả version).
        Task<FileUploadResultDTO> UploadAsync(
            UploadFileDTO dto, Stream content, string originalFileName, Guid actorId, bool isSystemAdmin,
            CancellationToken ct = default);

        // Hỏi TRƯỚC khi tải bytes: tên tài liệu này còn trống không, bận thì bận ở đâu, và người dùng
        // còn lựa chọn nào (lên phiên bản / tách tài liệu riêng kèm tên gợi ý). Không ghi gì.
        Task<NameAvailabilityDTO> CheckNameAvailabilityAsync(
            Guid folderId, string name, string format, bool bypassNamingConvention,
            Guid actorId, bool isSystemAdmin, CancellationToken ct = default);

        // Tải file về: kiểm tra quyền Download rồi mở luồng đọc phiên bản hiện hành.
        Task<DownloadFileResult> OpenDownloadAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);

        Task<DownloadFileResult> OpenVersionDownloadAsync(
            Guid fileItemId, Guid versionStateId, Guid actorId, CancellationToken ct = default);

        // Link xem/tải tạm thời (pre-signed) cho phiên bản hiện hành. null nếu đang lưu local.
        Task<string?> GetViewUrlAsync(Guid fileItemId, Guid actorId, int minutes = 60, CancellationToken ct = default);
    }

    public record DownloadFileResult(Stream Content, string FileName, string ContentType);
}
