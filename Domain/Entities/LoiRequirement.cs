using Domain.Enum.Loi;

namespace Domain.Entities
{
    // Một dòng = một ô đánh dấu trong bảng Phụ lục 02.
    // Bảng có 2 chiều: FieldName = nhãn dòng (để hiển thị), ParamName = tham số chuẩn (để dò trong IFC).
    // Một FieldName trải trên nhiều ParamName -> nhiều dòng cùng FieldName, chỉ cần khớp một là đạt.
    public class LoiRequirement
    {
        public Guid Id { get; set; }

        public LoiDiscipline Discipline { get; set; }

        // Mã OmniClass: 21 01/02/03 = KT-KC, 21 04 = MEP.
        public string? ComponentCode { get; set; }

        public string? ComponentName { get; set; }

        // Biến thể trong cùng mã ("Sàn bê tông" / "Sàn ngăn cháy"); null = mã chỉ có một bộ trường.
        public string? Variant { get; set; }

        public string FieldName { get; set; } = null!;

        public string FieldNameNormalized { get; set; } = null!;

        public string ParamName { get; set; } = null!;

        public string ParamNameNormalized { get; set; } = null!;

        public LoiParamGroup ParamGroup { get; set; }

        public LoiStage Stage { get; set; }
    }
}
