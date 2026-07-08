using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.SmartCA;

namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Khai bao cac nghiep vu VNPT SmartCA gan voi approval request.
    /// </summary>
    public interface IVnptSmartCaService
    {
        /// <summary>
        /// Lay danh sach chung thu so cua user tu VNPT SmartCA.
        /// </summary>
        Task<ApiResponse> GetCertificatesAsync(
            Guid approvalId,
            GetCertificateRequestDto request,
            Guid currentUserId);

        /// <summary>
        /// Tao giao dich ky so cho approval request.
        /// </summary>
        Task<ApiResponse> SendSignRequestAsync(
            Guid approvalId,
            SendSignRequestDto request,
            Guid currentUserId);

        /// <summary>
        /// Kiem tra trang thai giao dich ky so theo transaction id.
        /// </summary>
        Task<ApiResponse> GetTransactionStatusAsync(
            Guid approvalId,
            string transactionId,
            Guid currentUserId);

        /// <summary>
        /// Lay thong tin giao dich ky moi nhat da luu trong he thong.
        /// </summary>
        Task<ApiResponse> GetApprovalSignatureAsync(
            Guid approvalId,
            Guid currentUserId);
    }
}
