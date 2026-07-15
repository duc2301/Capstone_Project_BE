using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    // Truy vấn dữ liệu cho File Versioning — chỉ đọc dữ liệu, không chứa business logic.
    // CRUD generic (insert dòng snapshot FileVersionState) đi qua IUnitOfWork.Repository<T>().
    public interface IFileVersionRepository
    {
        // Nhận diện "tài liệu đã tồn tại": hiện tại CHỈ dựa trên FileItem.Name trong cùng folder.
        // Tương lai đổi sang FileNamingMetadata thì chỉ cần thay implementation của method này.
        Task<FileItem?> FindExistingDocumentAsync(Guid folderId, string fileName);

        // Dòng trạng thái hiện hành (IsCurrent = true) — tracked để service retire (IsCurrent = false)
        // rồi SaveChangesAsync khi insert dòng snapshot mới.
        Task<FileVersionState?> GetCurrentStateAsync(Guid fileItemId);

        // Toàn bộ lịch sử version của tài liệu, mới nhất trước (read-only).
        Task<List<FileVersionState>> GetHistoryAsync(Guid fileItemId);

        // Bản FileVersion vật lý hiện hành (FileItem.CurrentVersionId) — nguồn dữ liệu snapshot.
        Task<FileVersion?> GetCurrentFileVersionAsync(Guid fileItemId);

        // Số bản FileVersion vật lý đã có — dùng seed WorkingVersion cho tài liệu cũ chưa có state
        Task<int> CountFileVersionsAsync(Guid fileItemId);
    }
}
