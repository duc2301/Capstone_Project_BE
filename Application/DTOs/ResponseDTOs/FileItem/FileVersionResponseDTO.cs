namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class FileVersionResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public int VersionNumber { get; set; }
        // Chuỗi version theo hệ mới, vd "P01.02" / "C01" (null với dữ liệu từ hệ cũ)
        public string? DisplayVersion { get; set; }
        public string StoragePath { get; set; } = null!;
        public long FileSizeBytes { get; set; }
        public string Format { get; set; } = null!;
        public string? Checksum { get; set; }
        public bool IsHidden { get; set; }
        public Guid? UploadedByAccountId { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime? UploadedAt { get; set; }
    }
}
