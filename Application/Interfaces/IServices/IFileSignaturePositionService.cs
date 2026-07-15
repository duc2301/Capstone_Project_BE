using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    // Quan ly vi tri dat chu ky truc quan tren file PDF (chi PDF, chi khi file dang o WIP).
    public interface IFileSignaturePositionService
    {
        Task<FileSignaturePositionResponseDTO> SaveAsync(Guid fileItemId, SaveSignaturePositionDTO dto, Guid actor);

        Task<FileSignaturePositionResponseDTO> GetAsync(Guid fileItemId);

        // Kich thuoc trang PDF thuc te -> FE dung de tinh ty le dat vi tri ky thay vi gia dinh A4 co dinh.
        Task<PdfPageInfoResponseDTO> GetPageInfoAsync(Guid fileItemId, int pageNumber = 1);
    }
}
