using Domain.Enum.File;

namespace Domain.Entities
{
    public class ApprovalRequestSigner
    {
        public Guid Id { get; set; }
        public Guid ApprovalRequestId { get; set; }
        public Guid? SignerAccountId { get; set; }
        public Guid? SignerGroupId { get; set; }
        public ApprovalRequestSignerStatus Status { get; set; } = ApprovalRequestSignerStatus.Pending;
        public DateTime? SignedAt { get; set; }
        public string? CertificateSerial { get; set; }

        public ApprovalRequest ApprovalRequest { get; set; } = null!;
        public Account? SignerAccount { get; set; }
        public Group? SignerGroup { get; set; }
    }
}
