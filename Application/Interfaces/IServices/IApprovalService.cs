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

        // Duyệt file đang chờ phê duyệt. viaSignatureCompletion=true khi được gọi tự động ngay sau khi
        // người ký hoàn tất chữ ký bắt buộc cuối cùng (actor lúc đó là signer, không phải Team Leader) —
        // bản thân việc ký đủ đã là điều kiện đủ để tự động hoàn tất, Leader không cần bấm Duyệt nữa.
        Task<ApprovalRequestResponseDTO> ApproveAsync(Guid id, Guid actorId, bool viaSignatureCompletion = false);

        // Từ chối file đang chờ phê duyệt (lý do bắt buộc).
        Task<ApprovalRequestResponseDTO> RejectAsync(Guid id, RejectApprovalRequestDTO dto, Guid actorId);

        // Bắt buộc actor là Team Leader active của team phụ trách file (ném 403 nếu không phải).
        Task RequireTeamLeaderAsync(Guid fileItemId, Guid actorId);

        // Bắt buộc folder của file đã được cấp CanApprove cho ít nhất 1 nhóm (ném 403 nếu chưa) — dùng
        // làm điều kiện tiên quyết trước khi cho ký số, không quan tâm actor là ai.
        Task RequireFolderHasApprovePermissionConfiguredAsync(Guid fileItemId);

        // Snapshot DTO hiện tại của 1 approval request, không check quyền — dùng nội bộ để broadcast realtime.
        Task<ApprovalRequestResponseDTO> GetSnapshotAsync(Guid approvalId);

        // Tất cả account có thể đang xem/quan tâm approval này (requester, team leader, signer) — dùng để broadcast realtime.
        Task<IReadOnlyCollection<Guid>> GetStakeholderAccountIdsAsync(Guid approvalId);
    }
}
