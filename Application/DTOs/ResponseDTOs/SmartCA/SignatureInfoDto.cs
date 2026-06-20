using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.SmartCA
{
    /// <summary>
    /// Thong tin giao dich ky so da luu cho approval request.
    /// </summary>
    public class SignatureInfoDto
    {
        public Guid ApprovalRequestId { get; set; }
        public Guid FileItemId { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string? CertificateSerial { get; set; }
        public Guid? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public SignatureTransactionStatus Status { get; set; }
    }
}
