using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiImportParameterDTO
    {
        public LoiDiscipline Discipline { get; set; }
        public string Name { get; set; } = string.Empty;
        public LoiParamGroup ParamGroup { get; set; }
        public int OrderIndex { get; set; }
    }

    public class LoiImportComponentDTO
    {
        public LoiDiscipline Discipline { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class LoiImportCellDTO
    {
        public string ParamName { get; set; } = string.Empty;
        public LoiStage Stage { get; set; }
    }

    public class LoiImportRowDTO
    {
        public LoiDiscipline Discipline { get; set; }
        public string ComponentCode { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string? Variant { get; set; }
        public int FieldOrder { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public List<LoiImportCellDTO> Cells { get; set; } = new();
    }

    public class LoiImportComponentDiffDTO
    {
        public LoiDiscipline Discipline { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public LoiImportStatus Status { get; set; }
        public int VariantCount { get; set; }
        public int RequirementCount { get; set; }
        public int CurrentRequirementCount { get; set; }
    }

    public class LoiImportPreviewDTO
    {
        public List<LoiImportParameterDTO> Parameters { get; set; } = new();
        public List<LoiImportComponentDTO> Components { get; set; } = new();
        public List<LoiImportRowDTO> Rows { get; set; } = new();

        public List<LoiImportComponentDiffDTO> Diffs { get; set; } = new();

        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int UnchangedCount { get; set; }
        public int MissingCount { get; set; }

        public int TotalCellCount { get; set; }

        public int TaxonomyOnlyCount { get; set; }

        public List<string> Warnings { get; set; } = new();
    }
}
