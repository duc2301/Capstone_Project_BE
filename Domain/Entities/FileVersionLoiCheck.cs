using Domain.Enum.Loi;

namespace Domain.Entities
{
    public class FileVersionLoiCheck
    {
        public Guid Id { get; set; }

        public Guid FileVersionId { get; set; }

        public LoiCheckStatus Status { get; set; }

        public LoiVerdict Verdict { get; set; }

        // Chỉ đòi trường có Stage <= giá trị này.
        public LoiStage TargetStage { get; set; } = LoiStage.SchematicDesign;

        public double CoveragePercent { get; set; }

        public int TotalElements { get; set; }

        public int ConformantElements { get; set; }

        // Không khai mã, hoặc mã không có trong Phụ lục 02.
        public int ElementsWithUnknownType { get; set; }

        // Mã hợp lệ nhưng chuẩn không quy định trường nào — không phải lỗi của model.
        public int ElementsNotCoveredByStandard { get; set; }

        public string? SchemaName { get; set; }

        public string? ParserUsed { get; set; }

        public string? MissingSummaryJson { get; set; }

        // Tham số lạ đọc được trong file, kèm gợi ý ánh xạ để người dùng xác nhận.
        public string? UnmappedSummaryJson { get; set; }

        public string? NotCoveredSummaryJson { get; set; }

        // Báo cáo theo lớp + cấu kiện vi phạm. Chỉ đọc kèm kết quả, không truy vấn rời -> để JSON.
        public string? SectionsJson { get; set; }

        public string? Error { get; set; }

        public DateTime? CheckedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // FK trỏ sang FileVersionStates (hệ versioning mới) — FileVersions cũ đang được gỡ bỏ
        public FileVersionState FileVersion { get; set; } = null!;
    }
}
