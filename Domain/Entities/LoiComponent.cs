using Domain.Enum.Loi;

namespace Domain.Entities
{
    public class LoiComponent
    {
        public Guid Id { get; set; }

        public LoiDiscipline Discipline { get; set; }

        public string Code { get; set; } = null!;

        public string CodeNormalized { get; set; } = null!;

        public string Name { get; set; } = null!;
    }
}
