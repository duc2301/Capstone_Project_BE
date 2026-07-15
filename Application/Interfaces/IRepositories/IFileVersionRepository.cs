using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    // Truy vấn dữ liệu cho File Versioning — chỉ đọc dữ liệu, không chứa business logic.
    // CRUD generic (tạo/lưu FileVersionState) đi qua IUnitOfWork.Repository<T>().
    public interface IFileVersionRepository
    {
        // Nhận diện "tài liệu đã tồn tại": hiện tại CHỈ dựa trên FileItem.Name trong cùng folder.
        // Tương lai đổi sang FileNamingMetadata thì chỉ cần thay implementation của method này.
        Task<FileItem?> FindExistingDocumentAsync(Guid folderId, string fileName);

        // Trạng thái versioning hiện hành của tài liệu (tracked — service sửa xong gọi SaveChangesAsync)
        Task<FileVersionState?> GetVersionStateAsync(Guid fileItemId);

        // Số bản FileVersion vật lý đã có — dùng seed WorkingVersion cho tài liệu cũ chưa có state
        Task<int> CountFileVersionsAsync(Guid fileItemId);
    }
}
