using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiCheckResponseDTO
    {
        public LoiCheckStatus Status { get; set; }
        public LoiVerdict Verdict { get; set; }
        public LoiStage TargetStage { get; set; }
        public double CoveragePercent { get; set; }
        public int TotalElements { get; set; }
        public int ConformantElements { get; set; }

        public int ElementsWithUnknownType { get; set; }

        public int ElementsNotCoveredByStandard { get; set; }

        public string? SchemaName { get; set; }
        public string? Error { get; set; }
        public DateTime? CheckedAt { get; set; }
        public List<LoiMissingFieldDTO> Missing { get; set; } = new();
        public List<LoiUnmappedParamDTO> Unmapped { get; set; } = new();
        public List<LoiUncoveredComponentDTO> NotCovered { get; set; } = new();

        public List<LoiSectionDTO> Sections { get; set; } = new();
    }           
}
