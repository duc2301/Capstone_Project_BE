using Domain.Enum.Loi;

namespace Domain.Entities
{
    // Cây phân loại của Phụ lục 02, gồm CẢ mã mà chuẩn không quy định trường LOI nào
    // -> phân biệt được "model không khai mã" với "chuẩn để trống".
    public class LoiComponent
    {
        public Guid Id { get; set; }

        public LoiDiscipline Discipline { get; set; }

        public string Code { get; set; } = null!;

        // Chỉ giữ chữ và số, vd "2101101010".
        public string CodeNormalized { get; set; } = null!;

        public string Name { get; set; } = null!;
    }
}
