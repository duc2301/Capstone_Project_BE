using Application.DTOs.RequestDTOs.PermissionMatrix;
using Application.DTOs.ResponseDTOs.PermissionMatrix;
using Domain.Enum.Cde;

namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Ma trận phân quyền (RACI) theo dự án. Chỉ admin/PM/PA/leader truy cập được.
    /// GET dựng lưới (cột = nhóm, hàng = cây thư mục/file đã lọc theo quyền View).
    /// PUT lưu các ô thay đổi, đi qua PermissionLevelMapper để đồng nhất hợp đồng lưu trữ.
    /// </summary>
    public interface IPermissionMatrixService
    {
        Task<PermissionMatrixResponseDTO> GetMatrixAsync(
            Guid projectId, Guid accountId, bool isSystemAdmin, CdeArea? area = null);

        Task<List<MatrixCellResultDTO>> SaveMatrixAsync(
            Guid projectId, SavePermissionMatrixDTO dto, Guid accountId, bool isSystemAdmin);
    }
}
