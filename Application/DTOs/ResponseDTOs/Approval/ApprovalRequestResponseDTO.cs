using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.Approval
{
    /// <summary>
    /// Thông tin trả về của một yêu cầu phê duyệt file.
    /// </summary>
    public class ApprovalRequestResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public string FileItemName { get; set; } = null!;
        public string? CurrentZone { get; set; }
        public string? TargetZone { get; set; }
        public bool RequiresSignature { get; set; }
        public bool IsSigned { get; set; }
        public IReadOnlyCollection<ApprovalRequestSignerResponseDTO> Signers { get; set; } = Array.Empty<ApprovalRequestSignerResponseDTO>();
        public Guid RequestedBy { get; set; }
        public string? RequestedByName { get; set; }
        public Guid? ApproverId { get; set; }
        public string? ApproverName { get; set; }
        // Tên các Team Leader active của nhóm phụ trách file — người sẽ nhận/duyệt yêu cầu này.
        // Chỉ có ý nghĩa khi Status còn Pending; rỗng khi đã Approved/Rejected (lúc đó dùng
        // ApproverName vì đã có người thực sự quyết định).
        public IReadOnlyCollection<string> PendingApproverNames { get; set; } = Array.Empty<string>();
        // AccountId tương ứng với PendingApproverNames — FE dùng để xác định CHÍNH XÁC actor hiện
        // tại có phải người thực sự phụ trách hay không (kể cả khi actor cũng là người gửi request),
        // thay vì chỉ đoán qua "có phải Leader của một nhóm nào đó trong dự án".
        public IReadOnlyCollection<Guid> PendingApproverAccountIds { get; set; } = Array.Empty<Guid>();
        public ApprovalRequestStatus Status { get; set; }
        public string? RejectReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
