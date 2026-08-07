using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileItem
{
    // Cách FE nên hiển thị "Xem chi tiết" 1 file:
    //  - "model"   : file thiết kế (IFC/CAD) -> mở APS viewer bằng Urn.
    //  - "inline"  : xem trực tiếp trên web (PDF/ảnh/text, hoặc Office đã convert sang PDF) bằng Url + ContentType.
    //  - "download": không xem trực tiếp được -> chỉ tải về.
    public class FileViewInfoDTO
    {
        public string Kind { get; set; } = null!;

        public CdeArea Area { get; set; }

        // Dùng khi Kind = "model".
        public string? Urn { get; set; }

        // Trạng thái dịch model (chỉ Kind = "model"): None/Pending/Processing/Ready/Failed.
        // Ready -> FE mở viewer bằng Urn ngay; Pending/Processing -> FE hiện "đang xử lý" + poll lại; Failed -> cho dịch lại.
        // Serialize ra SỐ (BE không dùng JsonStringEnumConverter) -> FE khai báo numeric union khớp.
        public ModelViewerStatus? ViewerStatus { get; set; }

        // % tiến độ dịch (vd "75% complete") khi đang Processing — để FE hiển thị.
        public string? ViewerProgress { get; set; }

        // Dùng khi Kind = "inline" (presigned URL của nội dung hoặc của bản PDF preview).
        public string? Url { get; set; }

        // MIME của nội dung inline (application/pdf, image/png, text/plain...) — FE chọn iframe/img/text.
        public string? ContentType { get; set; }

        public string FileName { get; set; } = null!;

        public string? Format { get; set; }

        public Guid FolderId { get; set; }

        public Guid ProjectId { get; set; }

        public Guid VersionStateId { get; set; }

        public string? DisplayVersion { get; set; }

        public bool IsCurrentVersion { get; set; }

        // AI phân tích của ĐÚNG phiên bản đang xem (per-version): tóm tắt + cờ nghi ngờ + lý do.
        // Lấy từ FileVersionState của bản đang xem -> xem bản cũ thì hiện tóm tắt/cảnh báo của bản cũ.
        public string? Description { get; set; }
        public bool? Warnning { get; set; }
        public string? WarnningMessage { get; set; }
    }
}
