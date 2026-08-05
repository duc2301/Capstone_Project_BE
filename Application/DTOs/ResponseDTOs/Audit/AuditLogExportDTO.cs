namespace Application.DTOs.ResponseDTOs.Audit
{
    public class AuditLogExportDTO
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = "text/csv";
    }
}
