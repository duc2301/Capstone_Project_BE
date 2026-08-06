namespace Application.Services
{
    /// <summary>
    /// Ghep ten tep tai ve tu Name + Format. Du lieu cu co the da luu san duoi trong Name,
    /// ghep may moc se ra "ban-ve.ifc.ifc" nen chi noi duoi khi con thieu.
    /// </summary>
    public static class FileDownloadNaming
    {
        public static string BuildFileName(string? name, string? format)
        {
            var baseName = (name ?? string.Empty).Trim();
            var extension = (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

            if (extension.Length == 0) return baseName;

            return baseName.EndsWith('.' + extension, StringComparison.OrdinalIgnoreCase)
                ? baseName
                : $"{baseName}.{extension}";
        }
    }
}
