using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    // Quyết định cách "Xem chi tiết" 1 file: APS viewer (thiết kế), inline (PDF/ảnh/text/Office-đã-convert) hoặc download.
    public interface IFileViewService
    {
        Task<FileViewInfoDTO> GetViewInfoAsync(Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default);

        // Dịch lại model (IFC/CAD) lên APS: reset trạng thái về Pending rồi đẩy vào hàng đợi nền. Dùng khi Failed.
        Task RetranslateAsync(Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default);

        // Trả bytes PDF hiệu dụng để FE render bằng pdf.js (markup 2D theo trang) — same-origin, né CORS/hết hạn presigned.
        //  PDF -> file gốc; Office (doc/xls/ppt) -> bản convert PDF (cache PreviewStoragePath). File khác -> ApiExceptionResponse.
        Task<InlinePdfResult> OpenViewPdfAsync(Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default);
    }

    // Nội dung PDF hiệu dụng để xem/markup inline (luôn ContentType application/pdf).
    public record InlinePdfResult(Stream Content, string FileName);
}
