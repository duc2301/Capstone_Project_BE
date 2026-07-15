using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Interfaces.IRepositories
{
    // Truy vấn dữ liệu cho cây thư mục CDE + kiểm tra quyền View của account trên folder.
    public interface IFolderTreeRepository
    {
        Task<bool> ProjectExistsAsync(Guid projectId);

        Task<Folder?> GetFolderByIdAsync(Guid folderId);

        // Toàn bộ folder (không phải template) của 1 dự án, lọc theo khu vực CDE nếu có.
        Task<List<Folder>> GetProjectFoldersAsync(Guid projectId, CdeArea? area);

        // Các folderId trong dự án mà account có quyền View
        // (qua GroupMember Active -> ProjectParticipant Active -> FolderPermission Active + CanView).
        Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId);

        // Account thuộc 1 group giữ vai trò ProjectAdmin (PM) đang Active trong dự án -> thấy toàn bộ cây.
        Task<bool> HasFullAccessAsync(Guid projectId, Guid accountId);

        // Account có quyền View trên 1 folder cụ thể (dùng khi click vào folder).
        Task<bool> CanViewFolderAsync(Guid folderId, Guid accountId);

        Task<List<FileItem>> GetFilesByFolderIdAsync(Guid folderId);

        Task<HashSet<Guid>> GetWarningFolderIdsAsync(Guid projectId);

        // Subfolder TRỰC TIẾP (1 cấp, không phải template) của 1 folder.
        Task<List<Folder>> GetChildFoldersAsync(Guid parentFolderId);
    }
}
