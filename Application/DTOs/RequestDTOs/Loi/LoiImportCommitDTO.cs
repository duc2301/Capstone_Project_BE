using System.ComponentModel.DataAnnotations;
using Application.DTOs.ResponseDTOs.Loi;
using Domain.Enum.Loi;

namespace Application.DTOs.RequestDTOs.Loi
{
    public class LoiImportCommitDTO
    {
        [EnumDataType(typeof(LoiImportMode))]
        public LoiImportMode Mode { get; set; }

        public Guid? TargetRuleSetId { get; set; }

        [MaxLength(200)]
        public string? NewRuleSetName { get; set; }

        [MaxLength(1000)]
        public string? NewRuleSetDescription { get; set; }

        public List<string> SelectedComponentCodes { get; set; } = new();

        public bool DeleteMissing { get; set; }

        public List<LoiImportParameterDTO> Parameters { get; set; } = new();

        public List<LoiImportComponentDTO> Components { get; set; } = new();

        public List<LoiImportRowDTO> Rows { get; set; } = new();
    }
}
