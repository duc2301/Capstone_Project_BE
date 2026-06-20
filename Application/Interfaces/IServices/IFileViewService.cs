using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    // Quyết định cách "Xem chi tiết" 1 file: APS viewer (thiết kế), inline (PDF/ảnh/text/Office-đã-convert) hoặc download.
    public interface IFileViewService
    {
        // actorId do controller lấy từ JWT (User.GetAccountId()) truyền vào (gate quyền Download).
        Task<FileViewInfoDTO> GetViewInfoAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);

        // Dịch lại model (IFC/CAD) lên APS: reset trạng thái về Pending rồi đẩy vào hàng đợi nền. Dùng khi Failed.
        Task RetranslateAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);
    }
}
