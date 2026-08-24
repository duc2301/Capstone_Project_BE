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

        // Các file account xem được qua quyền RIÊNG TỪNG FILE (FileViewGrant / FilePermission CanView)
        // nhưng folder chứa lại không View được -> cần kéo lên tổ tiên View được để không bị ẩn khỏi cây.
        Task<List<FileItem>> GetExtraViewableFilesAsync(
            Guid projectId, Guid accountId, HashSet<Guid> viewableFolderIds);

        // Account thuộc 1 group giữ vai trò ProjectAdmin (PM) đang Active trong dự án -> thấy toàn bộ cây.
        Task<bool> HasFullAccessAsync(Guid projectId, Guid accountId);

        // Account là manager của dự án (Project.ManagerAccountId) -> full access như admin hệ thống (kể cả WIP).
        Task<bool> IsProjectManagerAsync(Guid projectId, Guid accountId);

        // Account có quyền View trên 1 folder cụ thể (dùng khi click vào folder).
        Task<bool> CanViewFolderAsync(Guid folderId, Guid accountId);

        Task<List<FileItem>> GetFilesByFolderIdAsync(Guid folderId);

        // Tổng số file của 1 folder — cơ sở phân trang cho nội dung folder.
        // excludeFileIds: bỏ qua các file bị từ chối xem ở cấp file (để đếm khớp phần đã lọc).
        Task<int> CountFilesByFolderIdAsync(Guid folderId, IReadOnlyCollection<Guid>? excludeFileIds = null);

        // 1 trang file của 1 folder (sắp theo Name tại DB, khớp GetFilesByFolderIdAsync).
        // excludeFileIds: loại các file bị từ chối xem TRƯỚC khi phân trang để offset luôn chính xác.
        Task<List<FileItem>> GetFilesByFolderIdPagedAsync(
            Guid folderId, int skip, int take, IReadOnlyCollection<Guid>? excludeFileIds = null);

        Task<HashSet<Guid>> GetWarningFolderIdsAsync(Guid projectId);

        // Subfolder TRỰC TIẾP (1 cấp, không phải template) của 1 folder.
        Task<List<Folder>> GetChildFoldersAsync(Guid parentFolderId);
    }
}
