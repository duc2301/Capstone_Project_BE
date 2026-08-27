using Domain.Enum.File;

namespace Application.Services
{
    public static class FileFormatRules
    {
        private static readonly IReadOnlyDictionary<FileType, string[]> ExtensionsByType =
            new Dictionary<FileType, string[]>
            {
                [FileType.Pdf] = new[] { ".pdf" },
                [FileType.Ifc] = new[] { ".ifc" },
                [FileType.Image] = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" },
                [FileType.Cad] = new[] { ".dwg", ".dxf", ".rvt", ".nwc", ".nwd", ".dgn" },
                [FileType.Office] = new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".txt" },
                [FileType.Other] = Array.Empty<string>(),
            };

        private static readonly IReadOnlyDictionary<string, FileType> TypeByExtension =
            ExtensionsByType
                .SelectMany(pair => pair.Value.Select(ext => (ext, pair.Key)))
                .ToDictionary(x => x.ext, x => x.Key, StringComparer.OrdinalIgnoreCase);

        public static string NormalizeExtension(string? format)
        {
            var value = (format ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length == 0) return string.Empty;
            return value.StartsWith('.') ? value : "." + value;
        }

        public static IReadOnlyList<string> ExtensionsOf(FileType type) =>
            ExtensionsByType.TryGetValue(type, out var extensions) ? extensions : Array.Empty<string>();

        public static FileType FromExtension(string? format) =>
            TypeByExtension.TryGetValue(NormalizeExtension(format), out var type) ? type : FileType.Other;
    }
}
