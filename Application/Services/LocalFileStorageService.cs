using System.Security.Cryptography;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    // Lưu file lên đĩa local. Gốc lưu trữ lấy từ cấu hình "FileStorage:RootPath",
    // mặc định <BaseDirectory>/App_Data/uploads. Bố cục: {root}/{projectId}/{folderId}/{guid}{ext}
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _root;

        public LocalFileStorageService(IConfiguration config)
        {
            var configured = config["FileStorage:RootPath"];
            _root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads")
                : configured;
        }

        public async Task<StoredFile> SaveAsync(
            Stream content, Guid projectId, Guid folderId, string extension, CancellationToken ct = default)
        {
            var dir = Path.Combine(_root, projectId.ToString(), folderId.ToString());
            Directory.CreateDirectory(dir);

            var ext = NormalizeExt(extension);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, fileName);

            long size;
            string checksum;
            using (var sha = SHA256.Create())
            await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var crypto = new CryptoStream(fs, sha, CryptoStreamMode.Write))
            {
                await content.CopyToAsync(crypto, ct);
                await crypto.FlushFinalBlockAsync(ct);
                size = fs.Length;
                checksum = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            }

            // Lưu path tương đối dùng '/' cho nhất quán đa nền tảng.
            var relative = string.Join('/', projectId.ToString(), folderId.ToString(), fileName);
            return new StoredFile(relative, size, checksum);
        }

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
        {
            var full = ToFullPath(storagePath);
            if (!File.Exists(full))
                throw new ApiExceptionResponse("Stored file not found on disk.", 404);
            Stream stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }

        // Đĩa local không có URL công khai -> trả null; caller dùng endpoint /download để tải qua server.
        public Task<string?> GetPresignedUrlAsync(string storagePath, int expiryMinutes = 60, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public string GetContentType(string fileNameOrExt)
        {
            var ext = NormalizeExt(Path.GetExtension(fileNameOrExt) is { Length: > 0 } e ? e : fileNameOrExt);
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".csv" => "text/csv",
                ".txt" => "text/plain",
                ".ifc" => "application/x-step",
                ".dwg" => "image/vnd.dwg",
                ".dxf" => "image/vnd.dxf",
                _ => "application/octet-stream"
            };
        }

        // Chặn path traversal: chỉ cho phép đường dẫn tương đối nằm trong gốc.
        private string ToFullPath(string relativePath)
        {
            var safe = relativePath.Replace('\\', '/').TrimStart('/');
            var full = Path.GetFullPath(Path.Combine(_root, safe));
            var rootFull = Path.GetFullPath(_root);
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new ApiExceptionResponse("Invalid storage path.", 400);
            return full;
        }

        private static string NormalizeExt(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
            ext = ext.Trim().ToLowerInvariant();
            return ext.StartsWith('.') ? ext : "." + ext;
        }
    }
}
