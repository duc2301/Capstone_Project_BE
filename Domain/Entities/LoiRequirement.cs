using Domain.Enum.Loi;

namespace Domain.Entities
{
    public class LoiRequirement
    {
        public Guid Id { get; set; }

        public LoiDiscipline Discipline { get; set; }

        public string? ComponentCode { get; set; }

        public string? ComponentName { get; set; }

        public string FieldName { get; set; } = null!;

        public string FieldNameNormalized { get; set; } = null!;

        public LoiParamGroup ParamGroup { get; set; }

        public int Stage { get; set; }

        public bool IsCommon { get; set; }
    }
}
