using Domain.Enum.File;

namespace Domain.Entities
{
    // Trạng thái versioning hiện hành của 1 tài liệu (1 dòng / 1 FileItem).
    // Bảng mới, độc lập với FileVersions (VersionNumber tuần tự) — không đụng bảng cũ.
    // FileVersionService đọc/ghi bảng này để tính P{Rev}.{Ver} / C{PubRev}.
    public class FileVersionState
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }

        // Giai đoạn hiện tại: Working (P) hoặc Published (C)
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

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public FileItem FileItem { get; set; } = null!;
    }
}
