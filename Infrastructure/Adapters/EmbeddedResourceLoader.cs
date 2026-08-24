using System.Reflection;

namespace Infrastructure.Adapters
{
    // Dùng chung cho các adapter cần đọc font nhúng (chữ ký số, watermark...).
    internal static class EmbeddedResourceLoader
    {
        public static byte[] LoadFontBytes(string fileName)
        {
            var assembly = typeof(EmbeddedResourceLoader).Assembly;
            var resourceName = $"Infrastructure.Resources.Fonts.{fileName}";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded font resource not found: {resourceName}");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
