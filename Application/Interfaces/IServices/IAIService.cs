using Application.DTOs.ResponseDTOs.Project;

namespace Application.Interfaces.IServices
{
    public interface IAIService
    {
        // Đọc nội dung file -> tóm tắt tiếng Việt cho người dùng đọc nhanh.
        // Trả null khi không tóm tắt được (không trích được chữ / AI lỗi) — advisory, không chặn flow.
        Task<string?> SummarizeContentAsync(Guid fileItemId, CancellationToken ct = default);

        // Đọc file BEP (stream upload, chưa lưu) -> trích các field prefill cho stepper khởi tạo dự án.
        // Không throw khi AI lỗi/không trích được chữ — trả DTO rỗng (ExtractionEmpty) để FE cho nhập tay.
        Task<BepParseResultDTO> ParseBepAsync(Stream content, string format, CancellationToken ct = default);
    }
}
