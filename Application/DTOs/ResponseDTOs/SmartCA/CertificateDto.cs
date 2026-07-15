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

        /// <summary>Chung thu DER dang base64, neu VNPT co tra ve (ten field khac nhau tuy phien ban API).</summary>
        public string? CertificateDataBase64 { get; set; }

        /// <summary>Cac chung thu chain (CA trung gian/goc) dang base64, neu VNPT tra kem trong response.</summary>
        public IReadOnlyList<string>? ChainCertificateDataBase64 { get; set; }
    }
}
