using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    // Quyết định cách "Xem chi tiết" 1 file: APS viewer (thiết kế), inline (PDF/ảnh/text/Office-đã-convert) hoặc download.
    public interface IFileViewService
    {
        Task<FileViewInfoDTO> GetViewInfoAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);

        Task<FileViewInfoDTO> GetVersionViewInfoAsync(
            Guid fileItemId, Guid versionStateId, Guid actorId, CancellationToken ct = default);

        // Dịch lại model (IFC/CAD) lên APS: reset trạng thái về Pending rồi đẩy vào hàng đợi nền. Dùng khi Failed.
        Task RetranslateAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);

        // Trả bytes PDF hiệu dụng để FE render bằng pdf.js (markup 2D theo trang) — same-origin, né CORS/hết hạn presigned.
        //  PDF -> file gốc; Office (doc/xls/ppt) -> bản convert PDF (cache PreviewStoragePath). File khác -> ApiExceptionResponse.
        Task<InlinePdfResult> OpenViewPdfAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);

        // Bytes nội dung để xem inline (PDF/ảnh/text, hoặc Office đã convert PDF) — proxy same-origin thay presigned URL public.
        //  Kiểm [Authorize] + CanViewFile theo TỪNG request nên link dán sang trình duyệt khác (không JWT) -> 401.
        //  versionStateId = null -> phiên bản hiện hành; có giá trị -> ghim đúng phiên bản (kể cả bản cũ).
        Task<InlineContentResult> OpenViewContentAsync(
            Guid fileItemId, Guid? versionStateId, Guid actorId, CancellationToken ct = default);
    }

    // Nội dung PDF hiệu dụng để xem/markup inline (luôn ContentType application/pdf).
    public record InlinePdfResult(Stream Content, string FileName);

    // Nội dung file để xem inline qua proxy same-origin (ContentType theo đúng loại: application/pdf, image/png, text/plain...).
    public record InlineContentResult(Stream Content, string ContentType, string FileName);
}
