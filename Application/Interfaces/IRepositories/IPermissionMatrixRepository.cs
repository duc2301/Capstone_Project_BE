using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    /// <summary>
    /// Truy cập dữ liệu (theo lô, tránh N+1) cho ma trận phân quyền. Tự sở hữu query riêng —
    /// không phụ thuộc module folder-tree — giống PermissionCheckingRepository.
    /// Các hàm "ForUpdate" trả về entity CÓ tracking để service sửa rồi CommitAsync qua UnitOfWork
    /// (cùng một CDESystemDbContext scoped). Các hàm còn lại là read-only (AsNoTracking).
    /// </summary>
    public interface IPermissionMatrixRepository
    {
        Task<bool> ProjectExistsAsync(Guid projectId);

        // ===== Cổng truy cập (leader). Admin/PM/PA đã có ở PermissionCheckingService. =====
        /// <summary>True nếu account là Leader (GroupMember) của ít nhất 1 nhóm đang tham gia dự án.</summary>
        Task<bool> IsLeaderInProjectAsync(Guid projectId, Guid accountId);

        /// <summary>
        /// True nếu account là Project Manager (Project.ManagerAccountId) của dự án. Dùng để mở khu vực
        /// Published/Archived trên ma trận — chỉ Admin hệ thống + PM được sửa quyền các vùng này (KHÔNG gồm PA).
        /// </summary>
        Task<bool> IsProjectManagerAsync(Guid projectId, Guid accountId);

        // ===== Cột =====
        /// <summary>Các bên tham gia đang hoạt động của dự án (kèm Group để lấy tên).</summary>
        Task<List<ProjectParticipant>> GetActiveParticipantsByProjectAsync(Guid projectId);

        /// <summary>
        /// Id các ProjectParticipant (trong dự án) mà caller là thành viên nhóm đang hoạt động —
        /// tức nhóm của chính caller. Dùng để loại nhóm caller khỏi ma trận (không tự sửa quyền nhóm mình).
        /// </summary>
        Task<HashSet<Guid>> GetCallerParticipantIdsAsync(Guid projectId, Guid accountId);

        // ===== Dữ liệu hàng =====
        /// <summary>Tất cả folder (không phải template) của dự án — để dựng cây cha/con + kiểm tra hợp lệ.</summary>
        Task<List<Folder>> GetProjectFoldersAsync(Guid projectId);

        /// <summary>File thuộc các folder cho trước (1 query).</summary>
        Task<List<FileItem>> GetFilesByFolderIdsAsync(IReadOnlyCollection<Guid> folderIds);

        /// <summary>File theo Id (để kiểm tra file thuộc dự án + lấy FolderId khi lưu).</summary>
        Task<List<FileItem>> GetFilesByIdsAsync(IReadOnlyCollection<Guid> fileIds);

        // ===== Giá trị ô hiện tại (chỉ dòng Active) =====
        Task<List<FolderPermission>> GetActiveFolderPermissionsByFolderIdsAsync(IReadOnlyCollection<Guid> folderIds);
        Task<List<FilePermission>> GetActiveFilePermissionsByFileIdsAsync(IReadOnlyCollection<Guid> fileIds);

        // ===== Lưu: nạp CÓ tracking để cập nhật =====
        /// <summary>FolderPermission (mọi Status) theo cặp folder × participant — tracking để sửa.</summary>
        Task<List<FolderPermission>> GetFolderPermissionsForUpdateAsync(
            IReadOnlyCollection<Guid> folderIds, IReadOnlyCollection<Guid> participantIds);

        /// <summary>FilePermission (mọi Status) theo cặp file × participant — tracking để sửa.</summary>
        Task<List<FilePermission>> GetFilePermissionsForUpdateAsync(
            IReadOnlyCollection<Guid> fileIds, IReadOnlyCollection<Guid> participantIds);
    }
}
