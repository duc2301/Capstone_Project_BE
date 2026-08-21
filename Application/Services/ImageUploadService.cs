using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class ImageUploadService : IImageUploadService
    {
        private const long MaxImageBytes = 5 * 1024 * 1024;
        private const int UrlExpiryMinutes = 60;

        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

        private readonly IFileStorageService _storage;
        private readonly ILogger<ImageUploadService> _logger;

        public ImageUploadService(IFileStorageService storage, ILogger<ImageUploadService> logger)
        {
            _storage = storage;
            _logger = logger;
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

            // Ảnh đại diện / ảnh dự án không nằm trong cây tài liệu CDE nên giữ nguyên bố cục phẳng
            // theo prefix caller truyền vào ("avatars/{accountId}", "project-images/{projectId}").
            var stored = await _storage.SaveAsync(content, new StorageObjectName(prefix, null, extension), ct);
            return stored.RelativePath;
        }

        public async Task<string?> GetImageUrlAsync(string? storagePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(storagePath)) return null;
            return await _storage.GetPresignedUrlAsync(storagePath, UrlExpiryMinutes, ct);
        }

        public async Task DeleteImageAsync(string? storagePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(storagePath)) return;

            try
            {
                await _storage.DeleteAsync(storagePath, ct);
            }
            catch (Exception ex)
            {
                // Ảnh mới đã lưu và bản ghi đã commit — người dùng thấy đúng ảnh mới. Xoá được ảnh cũ
                // hay không chỉ là chuyện dọn rác, không đáng để làm hỏng request.
                _logger.LogWarning(ex, "Không xoá được ảnh cũ {StoragePath} sau khi thay ảnh.", storagePath);
            }
        }
    }
}
