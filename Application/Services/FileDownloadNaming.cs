using System.Globalization;
using System.Text;

namespace Application.Services
{
    /// <summary>
    /// Ghep ten tep tai ve tu Name + Format. Du lieu cu co the da luu san duoi trong Name,
    /// ghep may moc se ra "ban-ve.ifc.ifc" nen chi noi duoi khi con thieu.
    /// </summary>
    public static class FileDownloadNaming
    {
        public const int SlugMaxLength = 60;

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

            var decomposed = value.Trim()
                .Replace('Đ', 'D')
                .Replace('đ', 'd')
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsAsciiLetterOrDigit(character))
                    builder.Append(char.ToLowerInvariant(character));
                else if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-');
            }

            var slug = builder.ToString().Trim('-');

            return slug.Length <= SlugMaxLength ? slug : slug[..SlugMaxLength].TrimEnd('-');
        }
    }
}
