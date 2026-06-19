using Domain.Enum.File;

namespace Domain.Entities
{
    /// <summary>
    /// Luu thong tin giao dich ky so VNPT SmartCA gan voi mot approval request va file CDE.
    /// </summary>
    public class ApprovalSignatureTransaction
    {
        public Guid Id { get; set; }
        public Guid ApprovalRequestId { get; set; }
        public Guid FileItemId { get; set; }
        public string TransactionId { get; set; } = null!;
        public string? CertificateSerial { get; set; }
        public string? Sad { get; set; }
        public Guid? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public SignatureTransactionStatus Status { get; set; } = SignatureTransactionStatus.Created;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Request gui di da duoc an cac thong tin nhay cam truoc khi luu.</summary>
        public string? RawRequest { get; set; }

        /// <summary>Response goc cua VNPT, dung de doi soat va debug loi tich hop.</summary>
        public string? RawResponse { get; set; }

        public ApprovalRequest ApprovalRequest { get; set; } = null!;
        public FileItem FileItem { get; set; } = null!;
        public Account? SignedByAccount { get; set; }
    }
}
