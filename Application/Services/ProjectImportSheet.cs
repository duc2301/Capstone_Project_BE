using System.Globalization;
using System.Text;

namespace Application.Services
{
    public static class ProjectImportSheet
    {
        public const string Guide = "HuongDan";
        public const string Project = "DuAn";
        public const string Packages = "GoiThau";
        public const string Groups = "Nhom";
        public const string Lookup = "TraCuu";

        public const int TitleRow = 1;
        public const int HeaderRow = 2;
        public const int FirstDataRow = 3;

        public const int LabelColumn = 1;
        public const int ValueColumn = 2;

        public const int MaxRows = 2000;
        public const int MaxColumns = 60;

        public const string FieldProjectName = "Tên dự án";
        public const string FieldProjectCode = "Mã dự án";
        public const string FieldProjectDescription = "Mô tả dự án";
        public const string FieldOwnerTaxCode = "MST chủ đầu tư";
        public const string FieldContactAddress = "Địa chỉ liên hệ";
        public const string FieldAddress = "Địa điểm công trình";
        public const string FieldLatitude = "Vĩ độ";
        public const string FieldLongitude = "Kinh độ";
        public const string FieldManagerEmail = "Email quản lý dự án";

        public const string ColumnPackageCode = "Mã gói";
        public const string ColumnPackageName = "Tên gói";
        public const string ColumnPackageDescription = "Mô tả";
        public const string ColumnWorkTypes = "Loại công việc";
        public const string ColumnScope = "Phạm vi công việc";
        public const string ColumnStartDate = "Ngày bắt đầu";
        public const string ColumnEndDate = "Ngày kết thúc";
        public const string ColumnContractValue = "Giá trị hợp đồng";
        public const string ColumnCurrency = "Tiền tệ";
        public const string ColumnTaxRate = "Thuế suất (%)";
        public const string ColumnContractorTaxCode = "MST nhà thầu";
        public const string ColumnContractNumber = "Số hợp đồng";
        public const string ColumnContractSignDate = "Ngày ký hợp đồng";
        public const string ColumnRepresentativeEmail = "Email người đại diện";
        public const string ColumnJobTitle = "Chức danh người đại diện";
        public const string ColumnNotes = "Ghi chú";

        public const string ColumnGroupName = "Tên nhóm";
        public const string ColumnGroupDescription = "Mô tả";
        public const string ColumnPartnerTaxCode = "MST đối tác";

        public static readonly string[] PackageHeaders =
        {
            ColumnPackageCode, ColumnPackageName, ColumnPackageDescription, ColumnWorkTypes,
            ColumnScope, ColumnStartDate, ColumnEndDate, ColumnContractValue, ColumnCurrency,
            ColumnTaxRate, ColumnContractorTaxCode, ColumnContractNumber, ColumnContractSignDate,
            ColumnRepresentativeEmail, ColumnJobTitle, ColumnNotes
        };

        public static readonly string[] GroupHeaders =
        {
            ColumnGroupName, ColumnGroupDescription, ColumnPartnerTaxCode
        };

        public static readonly string[] ProjectFields =
        {
            FieldProjectName, FieldProjectCode, FieldProjectDescription, FieldOwnerTaxCode,
            FieldContactAddress, FieldAddress, FieldLatitude, FieldLongitude, FieldManagerEmail
        };

        public const string DateFormat = "yyyy-MM-dd";

        private static readonly string[] DateFormats =
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd", "MM/dd/yyyy"
        };

        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var lastWasSpace = false;

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;

                var normalizedChar = character is 'đ' or 'Đ' ? 'd' : character;

                if (char.IsLetterOrDigit(normalizedChar))
                {
                    builder.Append(normalizedChar);
                    lastWasSpace = false;
                }
                else if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        public static bool TryParseDate(string? text, out DateTime value)
        {
            value = default;
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return false;

            if (DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out value))
                return true;

            return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        public static bool TryParseDecimal(string? text, out decimal value)
        {
            value = 0m;
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return false;

            var negative = trimmed.StartsWith('-');
            var cleaned = new string(trimmed.Where(c => char.IsDigit(c) || c is '.' or ',').ToArray());
            if (cleaned.Length == 0) return false;

            var canonical = MergeSeparators(cleaned);
            if (!decimal.TryParse(canonical, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return false;

            if (negative) value = -value;
            return true;
        }

        private static string MergeSeparators(string cleaned)
        {
            var dotCount = cleaned.Count(c => c == '.');
            var commaCount = cleaned.Count(c => c == ',');
            if (dotCount + commaCount == 0) return cleaned;

            var decimalSeparatorAt = -1;

            if (dotCount > 0 && commaCount > 0)
            {
                decimalSeparatorAt = Math.Max(cleaned.LastIndexOf('.'), cleaned.LastIndexOf(','));
            }
            else if (dotCount + commaCount == 1)
            {
                var only = cleaned.LastIndexOfAny(new[] { '.', ',' });
                if (cleaned.Length - only - 1 != 3) decimalSeparatorAt = only;
            }

            if (decimalSeparatorAt < 0)
                return cleaned.Replace(".", string.Empty).Replace(",", string.Empty);

            var integerPart = cleaned[..decimalSeparatorAt].Replace(".", string.Empty).Replace(",", string.Empty);
            var fractionPart = cleaned[(decimalSeparatorAt + 1)..].Replace(".", string.Empty).Replace(",", string.Empty);

            if (integerPart.Length == 0) integerPart = "0";
            return fractionPart.Length == 0 ? integerPart : $"{integerPart}.{fractionPart}";
        }

        public static bool TryParseDouble(string? text, out double value)
        {
            value = 0d;
            if (!TryParseDecimal(text, out var decimalValue)) return false;
            value = (double)decimalValue;
            return true;
        }
    }
}
