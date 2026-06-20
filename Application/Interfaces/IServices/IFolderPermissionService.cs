using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;
using Domain.Enum.Cde;

namespace Application.Interfaces.IServices
{
    // Nguồn chân lý duy nhất cho quyền truy cập thư mục CDE.
    // Quyền hiệu lực = bypass (Admin/PM) -> baseline theo khu vực + quyền sở hữu
    //                  -> gộp thêm các dòng override tường minh (FolderPermission).
    public interface IFolderPermissionService
    {
        // Quyền hiệu lực của 1 account trên 1 folder.
        Task<EffectivePermissionDTO> EvaluateAsync(Guid accountId, Guid folderId);

        // Ném 403 nếu account không có quyền 'action' trên folder.
        Task RequireAsync(Guid accountId, Guid folderId, FolderAction action);

        // Cây thư mục của dự án đã lọc theo quyền View của account (lọc theo khu vực nếu có).
        Task<List<FolderTreeNodeDTO>> GetTreeAsync(Guid projectId, Guid accountId, CdeArea? area = null);

        // Liệt kê các dòng ACL override tường minh trên 1 folder (chỉ Admin/PM — actor lấy từ JWT).
        Task<List<FolderPermissionResponseDTO>> GetPermissionsAsync(Guid folderId, Guid actorId, string? actorRole);

        // Upsert 1 dòng ACL override (theo cặp Folder + Group|Organization).
        Task<FolderPermissionResponseDTO> SetPermissionAsync(Guid folderId, SetFolderPermissionDTO dto, Guid actorId, string? actorRole);

        // Xóa 1 dòng ACL override.
        Task DeletePermissionAsync(Guid folderId, Guid permissionId, Guid actorId, string? actorRole);
    }
}
