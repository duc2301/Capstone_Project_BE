using Application.DTOs.ApiResponseDTO;

namespace Application.Interfaces.IServices
{
    // Sinh ban PDF da ky truc quan sau khi VNPT SmartCA tra trang thai Signed,
    // va phuc vu tai ve ban PDF da ky.
    public interface IPdfSignatureService
    {
        // Stamp chu ky truc quan vao PDF goc, tao FileVersion moi va danh dau FileItem da ky.
        // Goi tu VnptSmartCaService (khi transaction chuyen Signed) hoac truc tiep tu API generate-signed-pdf.
        Task<ApiResponse> GenerateSignedPdfAsync(Guid approvalId, Guid actor);

        // Tra metadata cua ban PDF da ky (khong phai noi dung file nhi phan) — FE dung de hien thi ket qua ky.
        Task<ApiResponse> GetSignedFileInfoAsync(Guid fileItemId, Guid actor);
    }
}
