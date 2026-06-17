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
        public Guid RequestedBy { get; set; }
        public string? RequestedByName { get; set; }
        public Guid? ApproverId { get; set; }
        public string? ApproverName { get; set; }
        public ApprovalRequestStatus Status { get; set; }
        public string? RejectReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
