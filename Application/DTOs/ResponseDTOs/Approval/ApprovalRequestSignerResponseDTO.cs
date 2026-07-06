using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.Approval
{
    public class ApprovalRequestSignerResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid? SignerAccountId { get; set; }
        public string? SignerAccountName { get; set; }
        public Guid? SignerGroupId { get; set; }
        public string? SignerGroupName { get; set; }
        public ApprovalRequestSignerStatus Status { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? CertificateSerial { get; set; }
    }
}
