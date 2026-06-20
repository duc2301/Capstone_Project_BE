using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.SmartCA
{
    /// <summary>
    /// Trang thai hien tai cua giao dich ky so VNPT SmartCA.
    /// </summary>
    public class TransactionStatusDto
    {
        public string TransactionId { get; set; } = string.Empty;
        public SignatureTransactionStatus Status { get; set; }
    }
}
