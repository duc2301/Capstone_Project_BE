namespace Application.DTOs.ResponseDTOs.FileItem
{
    // Metadata cua ban PDF da ky truc quan (khong phai noi dung file nhi phan).
    // Tai noi dung file thuc te dung endpoint download hien co (CurrentVersionId da tro ve ban da ky sau khi ky).
    public class SignedFileInfoResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public string? FileName { get; set; }
        public Guid SignedVersionId { get; set; }
        public int VersionNumber { get; set; }
        public string? StoragePath { get; set; }
        public string? Url { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? SignedBy { get; set; }
        public string? CertificateSerial { get; set; }
        public string? TransactionId { get; set; }
    }
}
