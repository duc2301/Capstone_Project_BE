namespace Application.DTOs.RequestDTOs.SmartCA
{
    /// <summary>
    /// Request lay chung thu so VNPT SmartCA cua user ky.
    /// </summary>
    public class GetCertificateRequestDto
    {
        /// <summary>Dinh danh user tren VNPT SmartCA, thuong la so dien thoai.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Serial chung thu can loc; de trong de lay tat ca chung thu cua user.</summary>
        public string? SerialNumber { get; set; }
    }
}
