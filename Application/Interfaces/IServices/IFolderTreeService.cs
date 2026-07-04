using Application.DTOs.ResponseDTOs.FileItem;
using Application.DTOs.ResponseDTOs.Folder;
using Domain.Enum.Cde;

namespace Application.Interfaces.IServices
{
    public interface IFolderTreeService
    {
        // Cây thư mục CDE của 1 dự án, chỉ gồm các folder mà account có quyền View
        // (Admin hệ thống / PM của dự án thấy toàn bộ cây).
        Task<List<FolderTreeNodeDTO>> GetTreeAsync(Guid projectId, Guid accountId, bool isSystemAdmin, CdeArea? area = null);

        // Danh sách file trong 1 folder khi user click vào folder đó; ném 403 nếu không có quyền View.
        Task<List<FileItemResponseDTO>> GetFilesByFolderAsync(Guid folderId, Guid accountId, bool isSystemAdmin);
    }
}
