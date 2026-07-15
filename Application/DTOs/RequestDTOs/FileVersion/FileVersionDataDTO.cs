using Domain.Enum.File;

namespace Application.DTOs.RequestDTOs.FileVersion
{
    // Dữ liệu file vật lý caller (Upload) truyền vào cho FileVersionService khi tạo version mới.
    // Versioning KHÔNG tự đọc từ hệ thống cũ (FileVersions) — mọi dữ liệu file đi qua DTO này.
    public class FileVersionDataDTO
    {
        public string? StoragePath { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? Format { get; set; }
        public string? Checksum { get; set; }
        public Guid? UploadedByAccountId { get; set; }

        // Model IFC/CAD cần dịch APS ngay khi upload -> Pending; còn lại None (dịch on-demand).
        public ModelViewerStatus ViewerStatus { get; set; } = ModelViewerStatus.None;
    }
}
