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
        // actorId do controller lấy từ JWT truyền vào (gate quyền Edit/Update + lưu tác giả version).
        Task<FileUploadResultDTO> UploadAsync(
            UploadFileDTO dto, Stream content, string originalFileName, Guid actorId, CancellationToken ct = default);

        // Tải file về: kiểm tra quyền Download rồi mở luồng đọc phiên bản hiện hành.
        Task<DownloadFileResult> OpenDownloadAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);

        // Link xem/tải tạm thời (pre-signed) cho phiên bản hiện hành. null nếu đang lưu local.
        Task<string?> GetViewUrlAsync(Guid fileItemId, Guid actorId, int minutes = 60, CancellationToken ct = default);
    }

    public record DownloadFileResult(Stream Content, string FileName, string ContentType);
}
