using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileVersion
{
    // 1 dòng lịch sử version: số version + snapshot dữ liệu file tại thời điểm đó.
    public class FileVersionHistoryItemDTO
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public bool IsCurrent { get; set; }

        public VersionStage Stage { get; set; }
        public int WorkingRevision { get; set; }
        public int WorkingVersion { get; set; }
        public int PublishedRevision { get; set; }
        public string DisplayVersion { get; set; } = null!;

        // Snapshot dữ liệu file tại thời điểm version này
        public Guid? FileVersionId { get; set; }
        public string? FileName { get; set; }
        public string? StoragePath { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? Format { get; set; }
        public string? Checksum { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
