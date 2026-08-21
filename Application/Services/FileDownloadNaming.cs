using System.Globalization;
using System.Text;

namespace Application.Services
{
    /// <summary>
    /// Ghep ten tep tai ve tu Name + Format. Du lieu cu co the da luu san duoi trong Name,
    /// ghep may moc se ra "ban-ve.ifc.ifc" nen chi noi duoi khi con thieu.
    /// Kem hai bo chuan hoa chuoi: ToSlug (ten tep tai ve) va ToKeySegment (segment cua object key).
    /// </summary>
    public static class FileDownloadNaming
    {
        public const int SlugMaxLength = 60;

        public const int KeySegmentMaxLength = 80;

        private const string TimestampFormat = "yyyyMMdd-HHmm";

        public static string BuildFileName(string? name, string? format)
        {
            var baseName = (name ?? string.Empty).Trim();
            var extension = (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

            if (extension.Length == 0) return baseName;

            return baseName.EndsWith('.' + extension, StringComparison.OrdinalIgnoreCase)
                ? baseName
                : $"{baseName}.{extension}";
        }

        public static string BuildTimestampedName(
            string prefix, string? scopeName, DateTime localTime, string extension)
        {
            var stamp = localTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            var slug = ToSlug(scopeName);
            var stem = slug.Length == 0 ? $"{prefix}-{stamp}" : $"{prefix}-{slug}-{stamp}";

            return $"{stem}.{extension.TrimStart('.').ToLowerInvariant()}";
        }

        public static string ToSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var character in RemoveDiacritics(value))
            {
                if (char.IsAsciiLetterOrDigit(character))
                    builder.Append(char.ToLowerInvariant(character));
                else if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-');
            }

            var slug = builder.ToString().Trim('-');

            return slug.Length <= SlugMaxLength ? slug : slug[..SlugMaxLength].TrimEnd('-');
        }

        /// <summary>
        /// Chuan hoa mot segment cua object key. Khac ToSlug o cho GIU NGUYEN hoa/thuong, dau cham
        /// va gach duoi — ma dinh danh ISO 19650 (HTM-ARC-L01-DR-A-0001) va nhan version (P01.02)
        /// phai doc duoc nguyen dang tren bucket. Van bo dau tieng Viet va thay ky tu la bang '-'
        /// de key chi con ASCII, tranh rac roi ky URL voi cac provider S3-compatible.
        /// </summary>
        public static string ToKeySegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var character in RemoveDiacritics(value))
            {
                if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_')
                    builder.Append(character);
                else if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-');
            }

            // Cat '.' o hai dau: tranh de lai dau cham thua ngay truoc phan duoi file.
            var segment = builder.ToString().Trim('-', '.');

            return segment.Length <= KeySegmentMaxLength
                ? segment
                : segment[..KeySegmentMaxLength].TrimEnd('-', '.');
        }

        // Tach dau tieng Viet ra khoi chu cai (NFD) roi bo cac dau combining; 'Đ/đ' khong phan ra
        // duoc bang NFD nen phai thay tay truoc.
        private static IEnumerable<char> RemoveDiacritics(string value)
        {
            var decomposed = value.Trim()
                .Replace('Đ', 'D')
                .Replace('đ', 'd')
                .Normalize(NormalizationForm.FormD);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    yield return character;
            }
        }
    }
}
