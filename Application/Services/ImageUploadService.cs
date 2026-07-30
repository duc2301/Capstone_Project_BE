using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;

namespace Application.Services
{
    public class ImageUploadService : IImageUploadService
    {
        private const long MaxImageBytes = 5 * 1024 * 1024;
        private const int UrlExpiryMinutes = 60;

        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

        private readonly IFileStorageService _storage;

        public ImageUploadService(IFileStorageService storage)
        {
            _storage = storage;
        }

        public async Task<string> SaveImageAsync(
            Stream content, string fileName, long sizeBytes, string prefix, CancellationToken ct = default)
        {
            if (sizeBytes <= 0)
                throw new ApiExceptionResponse("Tệp ảnh rỗng.", 400);

            if (sizeBytes > MaxImageBytes)
                throw new ApiExceptionResponse(
                    $"Ảnh vượt quá dung lượng cho phép ({MaxImageBytes / (1024 * 1024)} MB).", 400);

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                throw new ApiExceptionResponse(
                    $"Định dạng ảnh không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AllowedExtensions)}.", 400);

            var stored = await _storage.SaveToPrefixAsync(content, prefix, extension, ct);
            return stored.RelativePath;
        }

        public async Task<string?> GetImageUrlAsync(string? storagePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(storagePath)) return null;
            return await _storage.GetPresignedUrlAsync(storagePath, UrlExpiryMinutes, ct);
        }
    }
}
