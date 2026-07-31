using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiCheckResponseDTO
    {
        public LoiCheckStatus Status { get; set; }
        public LoiVerdict Verdict { get; set; }
        public double CoveragePercent { get; set; }
        public int TotalElements { get; set; }
        public int ConformantElements { get; set; }
        public int ElementsWithUnknownType { get; set; }
        public string? SchemaName { get; set; }
        public string? Error { get; set; }
        public DateTime? CheckedAt { get; set; }

        public List<LoiMissingFieldDTO> Missing { get; set; } = new();
    }

    public class LoiMissingFieldDTO
    {
        public string FieldName { get; set; } = string.Empty;
        public LoiParamGroup Group { get; set; }
        public int Stage { get; set; }
        public int MissingCount { get; set; }
    }
}
