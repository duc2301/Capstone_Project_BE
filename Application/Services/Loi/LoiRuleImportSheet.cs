using Domain.Enum.Loi;

namespace Application.Services.Loi
{
    public static class LoiRuleImportSheet
    {
        public const string Guide = "HuongDan";
        public const string Parameters = "ThamSo";
        public const string Components = "CauKien";
        public const string RequirementsKienTrucKetCau = "YeuCau-KTKC";
        public const string RequirementsMep = "YeuCau-MEP";

        public const int TitleRow = 1;
        public const int GroupRow = 2;
        public const int HeaderRow = 3;
        public const int FirstDataRow = 4;

        public const int CodeColumn = 1;
        public const int NameColumn = 2;
        public const int VariantColumn = 3;
        public const int FieldColumn = 4;
        public const int FirstParamColumn = 5;

        public const int MaxRows = 20000;
        public const int MaxParamColumns = 200;

        public static readonly IReadOnlyDictionary<string, LoiDiscipline> RequirementSheets =
            new Dictionary<string, LoiDiscipline>(StringComparer.OrdinalIgnoreCase)
            {
                [RequirementsKienTrucKetCau] = LoiDiscipline.KienTrucKetCau,
                [RequirementsMep] = LoiDiscipline.Mep
            };

        public static string DisciplineName(LoiDiscipline discipline) =>
            discipline == LoiDiscipline.Mep ? "MEP" : "Kiến trúc - Kết cấu";

        public static LoiDiscipline? ParseDiscipline(string? text)
        {
            var value = text?.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            if (value.Equals("MEP", StringComparison.OrdinalIgnoreCase)) return LoiDiscipline.Mep;
            if (value.Equals("1", StringComparison.Ordinal)) return LoiDiscipline.Mep;
            if (value.Equals("0", StringComparison.Ordinal)) return LoiDiscipline.KienTrucKetCau;
            if (value.StartsWith("Kiến trúc", StringComparison.OrdinalIgnoreCase)) return LoiDiscipline.KienTrucKetCau;
            return null;
        }

        public static string GroupName(LoiParamGroup group) => group switch
        {
            LoiParamGroup.DinhDanh => "Tham số định danh",
            LoiParamGroup.DinhVi => "Tham số định vị",
            LoiParamGroup.HinhHoc => "Tham số hình học",
            LoiParamGroup.QuyCach => "Quy cách kỹ thuật",
            LoiParamGroup.VatLieu => "Tham số vật liệu",
            _ => string.Empty
        };

        public static LoiParamGroup? ParseGroup(string? text)
        {
            var value = text?.Trim();
            if (string.IsNullOrEmpty(value)) return null;

            foreach (var group in System.Enum.GetValues<LoiParamGroup>())
                if (GroupName(group).Equals(value, StringComparison.OrdinalIgnoreCase))
                    return group;

            return int.TryParse(value, out var number) && System.Enum.IsDefined((LoiParamGroup)number)
                ? (LoiParamGroup)number
                : null;
        }
    }
}
