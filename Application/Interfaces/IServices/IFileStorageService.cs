namespace Application.Interfaces.IServices
{
    // Kho lưu nội dung file (bytes). Có nhiều implementation: đĩa local, S3 (Viettel Cloud Object Storage)…
    // Tầng nghiệp vụ chỉ phụ thuộc abstraction này; StoragePath = đường dẫn tương đối (local) hoặc object key (S3).
    public interface IFileStorageService
    {
        // Ghi nội dung vào vị trí do tầng nghiệp vụ chỉ định (xem ICdeStorageKeyBuilder).
        Task<StoredFile> SaveAsync(Stream content, StorageObjectName name, CancellationToken ct = default);

        // Mở luồng đọc theo StoragePath đã lưu trong FileVersionState.
        Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);

        // URL truy cập tạm thời (pre-signed) để xem/tải thẳng. null nếu provider không hỗ trợ (vd đĩa local).
        Task<string?> GetPresignedUrlAsync(string storagePath, int expiryMinutes = 60, CancellationToken ct = default);

        Task DeleteAsync(string storagePath, CancellationToken ct = default);

        string GetContentType(string fileNameOrExt);
    }

    // Vị trí + tên gợi ý của object sắp ghi. Adapter chỉ ghép chuỗi rồi ghi bytes — nó KHÔNG biết
    // project/folder/version là gì; chính sách bố cục nằm ở tầng nghiệp vụ (ICdeStorageKeyBuilder).
    // Stem = phần tên đọc được (đã bỏ dấu, chỉ còn ASCII); null -> adapter tự sinh tên ngẫu nhiên.
    // Adapter LUÔN gắn thêm hậu tố duy nhất nên hai lần ghi không bao giờ đè lên nhau.
    public sealed record StorageObjectName(string Prefix, string? Stem, string Extension);

    // Kết quả lưu file: path/key + dung lượng + checksum (SHA-256).
    public record StoredFile(string RelativePath, long SizeBytes, string Checksum);
}
