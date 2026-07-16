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

        /// <summary>Duong dan file PDF da stamp truc quan + dat cho /Contents (PdfTwoPhaseSigner), luu tam cho toi khi VNPT ky xong.</summary>
        public string? PreparedPdfStoragePath { get; set; }

        /// <summary>Hash (base64) cua PDF thuc su (document digest tu PdfTwoPhaseSigner) - dung de doi soat/debug.</summary>
        public string? DigestBase64 { get; set; }

        /// <summary>
        /// Authenticated attributes (CAdES, DER, base64) da dung o Phase 1 - day moi la du lieu thuc su
        /// duoc bam SHA-256 va gui cho VNPT ky (khong phai document digest truc tiep). Bat buoc phai luu
        /// lai vi khong tinh lai giong het duoc o Phase 2 (attribute nay co chua signing-time).
        /// </summary>
        public string? SignedAttributesBase64 { get; set; }

        /// <summary>
        /// Chu ky tho (raw signature, base64) VNPT tra ve khi transaction chuyen Signed - giai ma 1 lan
        /// luc do (GetTransactionStatusAsync) va luu lai, tranh PdfSignatureService phai tu parse RawResponse.
        /// </summary>
        public string? SignatureValueBase64 { get; set; }

        /// <summary>Chung thu (DER, base64) cua nguoi ky, lay tu get_certificate luc gui yeu cau ky.</summary>
        public string? SignerCertificateBase64 { get; set; }

        /// <summary>Thuat toan hash da dung de ky (mac dinh SHA-256).</summary>
        public string? HashAlgorithm { get; set; }

        public ApprovalRequest ApprovalRequest { get; set; } = null!;
        public FileItem FileItem { get; set; } = null!;
        public Account? SignedByAccount { get; set; }
    }
}
