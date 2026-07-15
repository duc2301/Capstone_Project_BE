using Domain.Enum.Loi;

namespace Domain.Entities
{
    public class FileVersionLoiCheck
    {
        public Guid Id { get; set; }

        public Guid FileVersionId { get; set; }

        public LoiCheckStatus Status { get; set; }

        public LoiVerdict Verdict { get; set; }

        public double CoveragePercent { get; set; }

        public int TotalElements { get; set; }

        public int ConformantElements { get; set; }

        public int ElementsWithUnknownType { get; set; }

        public string? SchemaName { get; set; }

        public string? ParserUsed { get; set; }

        public string? MissingSummaryJson { get; set; }

        public string? Error { get; set; }

        public DateTime? CheckedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public FileVersion FileVersion { get; set; } = null!;
    }
}
