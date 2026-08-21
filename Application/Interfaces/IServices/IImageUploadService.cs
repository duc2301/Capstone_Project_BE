namespace Application.Interfaces.IServices
{
    public interface IImageUploadService
    {
        Task<string> SaveImageAsync(Stream content, string fileName, long sizeBytes, string prefix, CancellationToken ct = default);

        Task<string?> GetImageUrlAsync(string? storagePath, CancellationToken ct = default);

        // Xoá ảnh cũ sau khi bản ghi đã trỏ sang ảnh mới. Bỏ qua nếu path rỗng.
        // PHẢI gọi SAU CommitAsync: commit hỏng thì ảnh cũ vẫn đang được tham chiếu, xoá sớm là mất ảnh.
        Task DeleteImageAsync(string? storagePath, CancellationToken ct = default);
    }
}
