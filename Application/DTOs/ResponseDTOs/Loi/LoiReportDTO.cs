using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiSectionDTO
    {
        public LoiCheckSection Section { get; set; }

        public LoiSeverity Severity { get; set; }

        public List<LoiRuleResultDTO> Rules { get; set; } = new();
    }

    public class LoiRuleResultDTO
    {
        public string Code { get; set; } = string.Empty;

        public LoiSeverity Severity { get; set; }

        public int OccurrenceCount { get; set; }

        public bool Truncated { get; set; }

        public List<LoiInstanceDTO> Instances { get; set; } = new();
    }

    public class LoiInstanceDTO
    {
        public string? GlobalId { get; set; }

        public string? Name { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public List<string> Details { get; set; } = new();
    }
}
