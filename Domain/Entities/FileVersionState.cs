using Domain.Enum.File;

namespace Domain.Entities
{
    // Bản ghi version HOÀN CHỈNH của tài liệu — thay thế bảng FileVersions cũ (đang gỡ dần).
    // Append-only: mỗi lần đổi version (upload/shared/publish/về WIP) INSERT 1 dòng mới,
    // KHÔNG update đè. Dòng IsCurrent = true là version hiện hành (đúng 1 dòng / FileItem).
    // Mỗi dòng vừa giữ số version (P/C) vừa giữ dữ liệu file vật lý + viewer + chữ ký của version đó.
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

        // --- Dữ liệu file vật lý của version này ---
        // FileVersionId: tham chiếu tạm sang bảng FileVersions cũ trong giai đoạn chuyển đổi —
        // sẽ bị xóa cùng bảng cũ ở bước cuối.
        public Guid? FileVersionId { get; set; }
        public string? FileName { get; set; }
        public string? StoragePath { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? Format { get; set; }
        public string? Checksum { get; set; }
        public bool IsHidden { get; set; }
        public Guid? UploadedByAccountId { get; set; }
        public DateTime? UploadedAt { get; set; }

        // --- Viewer / dịch model APS (chỉ dùng cho FileType Ifc/Cad) ---
        public string? ViewerUrn { get; set; }
        public string? PreviewStoragePath { get; set; }
        public ModelViewerStatus ViewerStatus { get; set; }
        public string? ViewerProgress { get; set; }
        public string? ViewerError { get; set; }

        // --- Chữ ký trực quan (visual signature) trên PDF ---
        public bool IsSigned { get; set; }
        public DateTime? SignedAt { get; set; }
        public Guid? SignedBy { get; set; }
        public string? CertificateSerial { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public FileItem FileItem { get; set; } = null!;
    }
}
