namespace Application.DTOs.RequestDTOs.SmartCA
{
    /// <summary>
    /// Request tao giao dich ky so VNPT SmartCA.
    /// </summary>
    public class SendSignRequestDto
    {
        /// <summary>Dinh danh user so huu chung thu so tren VNPT SmartCA.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Serial chung thu so duoc chon de ky.</summary>
        public string CertificateSerial { get; set; } = string.Empty;
    }
}
