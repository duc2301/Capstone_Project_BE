using Domain.Enum.File;

namespace Domain.Entities
{
    // Phiên bản file: trùng tên + định dạng -> tạo bản mới, ẩn bản cũ, phục hồi được
    public class FileVersion
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public int VersionNumber { get; set; }
        public string StoragePath { get; set; } = null!;
        public long FileSizeBytes { get; set; }
        public string Format { get; set; } = null!;
        public string? Checksum { get; set; }
        public bool IsHidden { get; set; }
        public Guid? UploadedByAccountId { get; set; }
        public DateTime? UploadedAt { get; set; }

        public string? ViewerUrn { get; set; }
        public string? PreviewStoragePath { get; set; }

        // --- Dịch model APS chạy nền (chỉ dùng cho FileType Ifc/Cad) ---
        // ViewerStatus điều khiển luồng "Xem chi tiết": Ready -> trả Urn mở ngay;
        // Pending/Processing -> FE hiện "đang xử lý" + poll; Failed -> cho dịch lại; None -> fallback dịch on-demand.
        public ModelViewerStatus ViewerStatus { get; set; }
        // % tiến độ dịch lấy từ manifest APS (vd "75% complete") để FE hiển thị mà không phải gọi APS.
        public string? ViewerProgress { get; set; }
        // Thông điệp lỗi khi ViewerStatus = Failed (không nuốt lỗi — để chẩn đoán/hiển thị).
        public string? ViewerError { get; set; }

        // --- Chu ky truc quan (visual signature) tren PDF ---
        public bool IsSigned { get; set; }
        public DateTime? SignedAt { get; set; }
        public Guid? SignedBy { get; set; }
        public string? CertificateSerial { get; set; }

        public FileItem FileItem { get; set; } = null!;
    }
}
