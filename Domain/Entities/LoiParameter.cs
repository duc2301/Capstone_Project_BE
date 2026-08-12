using Domain.Enum.Loi;

namespace Domain.Entities
{
    public class LoiParameter
    {
        public Guid Id { get; set; }

        public Guid RuleSetId { get; set; }

        public LoiDiscipline Discipline { get; set; }

        public string Name { get; set; } = null!;

        public string NameNormalized { get; set; } = null!;

        public LoiParamGroup ParamGroup { get; set; }

        public int OrderIndex { get; set; }

        public LoiRuleSet? RuleSet { get; set; }
    }
}
