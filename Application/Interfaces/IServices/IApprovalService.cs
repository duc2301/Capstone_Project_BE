using Application.DTOs.RequestDTOs.Approval;
using Application.DTOs.ResponseDTOs.Approval;

namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Xử lý các chức năng phê duyệt file CDE.
    /// </summary>
    /// <remarks>
    /// Approval gắn với FileItem, không gắn với Document vì Document đang dùng cho RAG.
    /// actorId của người thao tác do controller lấy từ JWT truyền vào.
    /// </remarks>
    public interface IApprovalService
    {
        // Gửi file để chờ Team Leader phê duyệt.
        Task<ApprovalRequestResponseDTO> SubmitAsync(Guid fileItemId, SubmitApprovalRequestDTO? dto, Guid actorId);

        // Tất cả yêu cầu phê duyệt mà người dùng được phép xem.
        Task<IEnumerable<ApprovalRequestResponseDTO>> GetAllAsync(Guid actorId);

        // Danh sách yêu cầu đang chờ duyệt của Team Leader hiện tại.
        Task<IEnumerable<ApprovalRequestResponseDTO>> GetPendingAsync(Guid actorId);

        // Chi tiết một yêu cầu phê duyệt.
        Task<ApprovalRequestResponseDTO> GetByIdAsync(Guid id, Guid actorId);

        // Duyệt file đang chờ phê duyệt.
        Task<ApprovalRequestResponseDTO> ApproveAsync(Guid id, Guid actorId);

        // Từ chối file đang chờ phê duyệt (lý do bắt buộc).
        Task<ApprovalRequestResponseDTO> RejectAsync(Guid id, RejectApprovalRequestDTO dto, Guid actorId);
    }
}
