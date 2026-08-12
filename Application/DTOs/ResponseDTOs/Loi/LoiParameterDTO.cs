using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiParameterDTO
    {
        public Guid Id { get; set; }

        public LoiDiscipline Discipline { get; set; }

        public string Name { get; set; } = string.Empty;

        public LoiParamGroup ParamGroup { get; set; }

        public int OrderIndex { get; set; }

        public int UsageCount { get; set; }
    }
}
