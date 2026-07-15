using Application.DTOs.ApiResponseDTO;
using Application.Services.Signing;

namespace Application.Interfaces.IServices
{
    // Sinh ban PDF da ky truc quan sau khi VNPT SmartCA tra trang thai Signed,
    // va phuc vu tai ve ban PDF da ky.
    public interface IPdfSignatureService
    {
        // Phase 1 cua ky 2 pha: ve khung "CHU KY SO" truc quan (goi kem ca nguoi dang cho ky nay) +
        // dat cho signature field, tra ve document digest + authenticated attributes (CAdES) can bam
        // va gui cho VNPT ky. Goi tu VnptSmartCaService.SendSignRequestAsync TRUOC khi goi API ky VNPT.
        Task<PdfExternalSignatureHelper.PreparedSignature> PrepareSignatureAsync(
            Guid approvalId,
            Guid pendingSignerId,
            string pendingCertificateSerial,
            byte[] pendingSignerCertificateDer,
            string pendingTransactionId);

        // Phase 2 + bookkeeping: nhung chu ky that (tu prepared data + signature VNPT tra ve) vao PDF,
        // tao FileVersion moi va danh dau FileItem da ky.
        // Goi tu VnptSmartCaService (khi transaction chuyen Signed) hoac truc tiep tu API generate-signed-pdf.
        Task<ApiResponse> GenerateSignedPdfAsync(Guid approvalId, Guid actor);

        // Tra metadata cua ban PDF da ky (khong phai noi dung file nhi phan) — FE dung de hien thi ket qua ky.
        Task<ApiResponse> GetSignedFileInfoAsync(Guid fileItemId, Guid actor);
    }
}
