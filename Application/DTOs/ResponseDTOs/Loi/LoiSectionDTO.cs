using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiSectionDTO
    {
        public LoiCheckSection Section { get; set; }

        public LoiSeverity Severity { get; set; }

        public List<LoiRuleResultDTO> Rules { get; set; } = new();
    }        
}
