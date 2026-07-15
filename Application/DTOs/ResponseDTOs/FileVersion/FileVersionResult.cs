using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileVersion
{
    // Kết quả tính version — caller (Upload/Publish/Zone transition) chỉ cần dùng và lưu lại.
    public class FileVersionResult
    {
        public Guid? FileItemId { get; set; }

        // true = tài liệu hoàn toàn mới (chưa có FileItem trùng tên) —
        // caller tạo FileItem xong gọi CreateInitialVersionAsync để chốt trạng thái.
        public bool IsNewDocument { get; set; }

        public VersionStage Stage { get; set; }

        public int WorkingRevision { get; set; }

        public int WorkingVersion { get; set; }

        public int PublishedRevision { get; set; }

        // Chuỗi hiển thị đã format: "P01.02" hoặc "C01"
        public string DisplayVersion { get; set; } = null!;
    }
}
