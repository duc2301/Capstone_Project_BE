using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;

namespace Infrastructure.Adapters.Storage
{
    // Ghép key/đường dẫn cuối cùng từ StorageObjectName. Dùng chung cho MỌI provider để hai adapter
    // (S3, đĩa local) không trôi khỏi nhau. Đảm bảo duy nhất là trách nhiệm của tầng này: luôn gắn
    // hậu tố ngẫu nhiên vào tên lá nên không lần ghi nào đè lên object đã có.
    internal static class StorageKeyComposer
    {
        // Đủ dài để hai lần ghi cùng prefix + cùng tên không đụng nhau, đủ ngắn để key còn đọc được.
        private const int UniqueSuffixLength = 8;

        public static string NormalizePrefix(string prefix)
        {
            var segments = (prefix ?? string.Empty)
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s != "." && s != "..")
                .ToArray();

            if (segments.Length == 0)
                throw new ApiExceptionResponse("Invalid storage prefix.", 400);

            return string.Join('/', segments);
        }

        // Tên lá: "{stem}__{8 hex}{ext}" khi caller đặt tên, "{32 hex}{ext}" khi không.
        public static string BuildLeafName(string? stem, string extension)
        {
            var ext = NormalizeExt(extension);
            var unique = Guid.NewGuid().ToString("N");
            var safeStem = SanitizeStem(stem);

            return safeStem.Length == 0
                ? $"{unique}{ext}"
                : $"{safeStem}__{unique[..UniqueSuffixLength]}{ext}";
        }

        public static string NormalizeExt(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
            var ext = extension.Trim().ToLowerInvariant();
            return ext.StartsWith('.') ? ext : "." + ext;
        }

        // Lưới an toàn cuối: caller đã slug hoá stem rồi, nhưng tên lá tuyệt đối không được chứa
        // dấu phân cách đường dẫn hay ký tự điều khiển — nếu lọt sẽ đẻ ra thư mục ngoài ý muốn.
        private static string SanitizeStem(string? stem)
        {
            if (string.IsNullOrWhiteSpace(stem)) return string.Empty;

            var cleaned = new string(stem
                .Where(c => !char.IsControl(c) && c != '/' && c != '\\')
                .ToArray())
                .Trim()
                .Trim('.');

            return cleaned;
        }
    }
}
