using Application.DTOs.RequestDTOs.Approval;
using Application.DTOs.ResponseDTOs.Approval;

namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Xử lý các chức năng phê duyệt file CDE.
    /// </summary>
    /// <remarks>
    /// Approval gắn với FileItem, không gắn với Document vì Document đang dùng cho RAG.
    /// </remarks>
    public interface IApprovalService
    {
        /// <summary>
        /// Gửi file để chờ Team Leader phê duyệt.
        /// </summary>
        /// <param name="fileItemId">Id của file cần gửi duyệt.</param>
        /// <returns>Thông tin yêu cầu phê duyệt vừa tạo.</returns>
        Task<ApprovalRequestResponseDTO> SubmitAsync(Guid fileItemId);

        /// <summary>
        /// Lấy tất cả yêu cầu phê duyệt mà người dùng hiện tại được phép xem.
        /// </summary>
        /// <returns>Danh sách yêu cầu phê duyệt gồm Pending, Approved và Rejected.</returns>
        Task<IEnumerable<ApprovalRequestResponseDTO>> GetAllAsync();

        /// <summary>
        /// Lấy danh sách yêu cầu đang chờ duyệt của Team Leader hiện tại.
        /// </summary>
        /// <returns>Danh sách yêu cầu có trạng thái Pending.</returns>
        Task<IEnumerable<ApprovalRequestResponseDTO>> GetPendingAsync();

        /// <summary>
        /// Lấy chi tiết một yêu cầu phê duyệt.
        /// </summary>
        /// <param name="id">Id của yêu cầu phê duyệt.</param>
        /// <returns>Chi tiết yêu cầu phê duyệt.</returns>
        Task<ApprovalRequestResponseDTO> GetByIdAsync(Guid id);

        /// <summary>
        /// Duyệt file đang chờ phê duyệt.
        /// </summary>
        /// <param name="id">Id của yêu cầu phê duyệt.</param>
        /// <returns>Thông tin yêu cầu sau khi duyệt.</returns>
        Task<ApprovalRequestResponseDTO> ApproveAsync(Guid id);

        /// <summary>
        /// Từ chối file đang chờ phê duyệt.
        /// </summary>
        /// <param name="id">Id của yêu cầu phê duyệt.</param>
        /// <param name="dto">Thông tin lý do từ chối.</param>
        /// <returns>Thông tin yêu cầu sau khi từ chối.</returns>
        Task<ApprovalRequestResponseDTO> RejectAsync(Guid id, RejectApprovalRequestDTO dto);
    }
}
