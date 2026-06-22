namespace Application.DTOs.ResponseDTOs.SmartCA
{
    /// <summary>
    /// Thong tin chung thu so tra ve tu VNPT SmartCA.
    /// </summary>
    public class CertificateDto
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string? Issuer { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string? Status { get; set; }
    }
}
