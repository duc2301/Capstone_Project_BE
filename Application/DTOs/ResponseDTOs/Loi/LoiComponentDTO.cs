using Domain.Enum.Loi;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiComponentDTO
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public LoiDiscipline Discipline { get; set; }

        public int VariantCount { get; set; }

        public int RequirementCount { get; set; }
    }
}
