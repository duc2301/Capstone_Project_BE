using Domain.Enum.File;

namespace Domain.Entities
{
    /// <summary>
    /// Yêu cầu phê duyệt của một file CDE.
    /// </summary>
    /// <remarks>
    /// FileItemId là file cần phê duyệt.
    /// RequestedBy là người gửi duyệt.
    /// ApproverId là Team Leader đã duyệt hoặc từ chối.
    /// </remarks>
    public class ApprovalRequest
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public Guid RequestedBy { get; set; }
        public Guid? ApproverId { get; set; }
        public ApprovalRequestStatus Status { get; set; }
        public string? RejectReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public FileItem FileItem { get; set; } = null!;
        public Account Requester { get; set; } = null!;
        public Account? Approver { get; set; }
    }
}
