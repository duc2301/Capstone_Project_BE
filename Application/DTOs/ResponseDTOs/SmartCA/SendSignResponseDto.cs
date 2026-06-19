using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.SmartCA
{
    /// <summary>
    /// Ket qua tao giao dich ky so VNPT SmartCA.
    /// </summary>
    public class SendSignResponseDto
    {
        public Guid ApprovalRequestId { get; set; }
        public Guid FileItemId { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string? Sad { get; set; }
        public SignatureTransactionStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
