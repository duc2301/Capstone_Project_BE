using Domain.Enum.File;

namespace Domain.Entities
{
    // Lịch sử versioning của tài liệu — MỖI lần đổi version (upload/shared/publish/về WIP)
    // INSERT 1 dòng mới kèm snapshot dữ liệu file tại thời điểm đó, KHÔNG update đè.
    // Dòng IsCurrent = true là trạng thái hiện hành (đúng 1 dòng / FileItem); các dòng còn lại là lịch sử.
    public class FileVersionState
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }

        // true = trạng thái version hiện hành; false = snapshot lịch sử
        public bool IsCurrent { get; set; } = true;

        // Giai đoạn tại thời điểm snapshot: Working (P) hoặc Published (C)
        public VersionStage Stage { get; set; } = VersionStage.Working;

        // Revision sau chữ P — tăng khi tài liệu vào SHARED thành công
        public int WorkingRevision { get; set; } = 1;

        // Số sau dấu chấm — tăng khi upload file thay thế trong cùng Working Revision,
        // reset về 1 khi sang Working Revision mới
        public int WorkingVersion { get; set; } = 1;

        // Revision phát hành (C01, C02, ...) — độc lập với Working Revision,
        // được bảo toàn khi tài liệu quay về WIP (0 = chưa từng publish)
        public int PublishedRevision { get; set; }

        // Chuỗi hiển thị đã format sẵn, vd "P01.02" hoặc "C01"
        public string DisplayVersion { get; set; } = null!;

        // --- Snapshot dữ liệu file tại thời điểm version này ---
        // Chép giá trị (không FK cứng sang FileVersions) để dòng lịch sử tự đủ,
        // không vỡ nếu bản ghi FileVersion vật lý bị ẩn/xóa sau này.
        public Guid? FileVersionId { get; set; }
        public string? FileName { get; set; }
        public string? StoragePath { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? Format { get; set; }
        public string? Checksum { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public FileItem FileItem { get; set; } = null!;
    }
}
