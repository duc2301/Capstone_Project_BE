namespace Application.Interfaces.IServices
{
    public interface IAIService
    {
        // Đọc nội dung file -> tóm tắt tiếng Việt cho người dùng đọc nhanh.
        // Trả null khi không tóm tắt được (không trích được chữ / AI lỗi) — advisory, không chặn flow.
        Task<string?> SummarizeContentAsync(Guid fileItemId, CancellationToken ct = default);
    }
}
