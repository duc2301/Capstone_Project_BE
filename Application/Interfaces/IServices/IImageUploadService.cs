namespace Application.Interfaces.IServices
{
    public interface IImageUploadService
    {
        Task<string> SaveImageAsync(Stream content, string fileName, long sizeBytes, string prefix, CancellationToken ct = default);

        Task<string?> GetImageUrlAsync(string? storagePath, CancellationToken ct = default);
    }
}
