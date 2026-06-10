namespace Application.Interfaces.IServices
{
    // Lưu trữ file ở mức hệ thống hiện có: đĩa local (chưa dùng cloud).
    // StoragePath trả về là đường dẫn TƯƠNG ĐỐI so với gốc cấu hình.
    public interface ILocalFileStorageService
    {
        Task<StoredFile> SaveAsync(Stream content, Guid projectId, Guid folderId, string extension, CancellationToken ct = default);

        // Mở luồng đọc theo đường dẫn tương đối đã lưu trong FileVersion.StoragePath.
        Stream OpenRead(string relativePath);

        bool Exists(string relativePath);

        string GetContentType(string fileNameOrExt);
    }

    // Kết quả lưu file: path tương đối + dung lượng + checksum (SHA-256).
    public record StoredFile(string RelativePath, long SizeBytes, string Checksum);
}
