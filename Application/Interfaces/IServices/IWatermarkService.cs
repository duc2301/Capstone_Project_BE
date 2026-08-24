using Domain.Enum.Cde;

namespace Application.Interfaces.IServices
{
    // Đóng watermark định danh người xem/tải để truy vết nếu file bị phát tán ra ngoài hệ thống.
    public interface IWatermarkService
    {
        // Định dạng chưa hỗ trợ (ảnh, CAD, pptx...) trả nguyên input, không đổi.
        Stream Stamp(Stream input, string format, string label);

        // Gộp sẵn chính sách: chỉ watermark khi area là Shared/Published, tự lấy Account theo actorId.
        // Trả về cùng reference nếu không đủ điều kiện -> so ReferenceEquals để biết có cần dispose không.
        Task<Stream> ApplyAsync(Stream input, string format, CdeArea? area, Guid actorId, CancellationToken ct = default);
    }
}
