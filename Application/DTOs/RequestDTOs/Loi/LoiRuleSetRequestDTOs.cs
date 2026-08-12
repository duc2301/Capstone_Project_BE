using System.ComponentModel.DataAnnotations;
using Domain.Enum.Loi;

namespace Application.DTOs.RequestDTOs.Loi
{
    public class CreateLoiRuleSetDTO
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }
    }

    public class UpdateLoiRuleSetDTO
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
    }

    public class CreateLoiComponentDTO
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Name { get; set; } = string.Empty;

        [EnumDataType(typeof(LoiDiscipline))]
        public LoiDiscipline Discipline { get; set; }
    }

    public class UpdateLoiComponentDTO
    {
        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(300)]
        public string? Name { get; set; }

        [EnumDataType(typeof(LoiDiscipline))]
        public LoiDiscipline? Discipline { get; set; }
    }

    public class CreateLoiParameterDTO
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [EnumDataType(typeof(LoiDiscipline))]
        public LoiDiscipline Discipline { get; set; }

        [EnumDataType(typeof(LoiParamGroup))]
        public LoiParamGroup ParamGroup { get; set; }

        public int OrderIndex { get; set; }
    }

    public class UpdateLoiParameterDTO
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [EnumDataType(typeof(LoiParamGroup))]
        public LoiParamGroup? ParamGroup { get; set; }

        public int? OrderIndex { get; set; }
    }

    public class SaveLoiMatrixCellDTO
    {
        [Required]
        [MaxLength(200)]
        public string ParamName { get; set; } = string.Empty;

        [EnumDataType(typeof(LoiStage))]
        public LoiStage Stage { get; set; }
    }

    public class SaveLoiMatrixRowDTO
    {
        [Required]
        [MaxLength(300)]
        public string FieldName { get; set; } = string.Empty;

        public List<SaveLoiMatrixCellDTO> Cells { get; set; } = new();
    }

    public class SaveLoiMatrixDTO
    {
        [MaxLength(300)]
        public string? Variant { get; set; }

        public List<SaveLoiMatrixRowDTO> Rows { get; set; } = new();
    }

    public class RenameLoiVariantDTO
    {
        [MaxLength(300)]
        public string? CurrentVariant { get; set; }

        [MaxLength(300)]
        public string? NewVariant { get; set; }
    }

    public class SetProjectLoiRuleSetDTO
    {
        public Guid? RuleSetId { get; set; }
    }

    public class CreateSystemLoiAliasDTO
    {
        [Required]
        [MaxLength(200)]
        public string ParamNameInModel { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string StandardParamName { get; set; } = string.Empty;
    }
}
