using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiMatrixCellDTO
    {
        public string ParamName { get; set; } = string.Empty;

        public LoiStage Stage { get; set; }
    }

    public class LoiMatrixRowDTO
    {
        public string FieldName { get; set; } = string.Empty;

        public List<LoiMatrixCellDTO> Cells { get; set; } = new();
    }

    public class LoiMatrixVariantDTO
    {
        public string? Variant { get; set; }

        public List<LoiMatrixRowDTO> Rows { get; set; } = new();
    }

    public class LoiMatrixDTO
    {
        public Guid RuleSetId { get; set; }

        public Guid ComponentId { get; set; }

        public string ComponentCode { get; set; } = string.Empty;

        public string ComponentName { get; set; } = string.Empty;

        public LoiDiscipline Discipline { get; set; }

        public List<LoiParameterDTO> Parameters { get; set; } = new();

        public List<LoiMatrixVariantDTO> Variants { get; set; } = new();
    }
}
