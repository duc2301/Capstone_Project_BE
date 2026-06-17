using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    // Luồng tải file lên (mức hệ thống hiện có — lưu đĩa local):
    //  ① đối chiếu quyền (Edit/Update) ② [linking - hoãn] ③ kiểm tra tên file
    //  ④ pre-upload consistency ⑤ kiểm tra version (trùng -> bản mới, đẩy bản cũ sang Archived)
    //  ⑦ lưu file vào đúng thư mục.
    public interface IFileUploadService
    {
        Task<FileUploadResultDTO> UploadAsync(
            UploadFileDTO dto, Stream content, string originalFileName, CancellationToken ct = default);

        // Tải file về: kiểm tra quyền Download rồi mở luồng đọc phiên bản hiện hành.
        Task<DownloadFileResult> OpenDownloadAsync(Guid fileItemId, CancellationToken ct = default);

        // Link xem/tải tạm thời (pre-signed) cho phiên bản hiện hành. null nếu đang lưu local.
        Task<string?> GetViewUrlAsync(Guid fileItemId, int minutes = 60, CancellationToken ct = default);
    }

    public record DownloadFileResult(Stream Content, string FileName, string ContentType);
}
