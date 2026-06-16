namespace Application.Interfaces.IServices
{
    // Kho lưu nội dung file (bytes). Có nhiều implementation: đĩa local, S3 (Viettel Cloud Object Storage)…
    // Tầng nghiệp vụ chỉ phụ thuộc abstraction này; StoragePath = đường dẫn tương đối (local) hoặc object key (S3).
    public interface IFileStorageService
    {
        Task<StoredFile> SaveAsync(Stream content, Guid projectId, Guid folderId, string extension, CancellationToken ct = default);

        // Mở luồng đọc theo StoragePath đã lưu trong FileVersion.
        Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);

        string GetContentType(string fileNameOrExt);
    }

    // Kết quả lưu file: path/key + dung lượng + checksum (SHA-256).
    public record StoredFile(string RelativePath, long SizeBytes, string Checksum);
}
