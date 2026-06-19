namespace Domain.Entities
{
    // Phiên bản file: trùng tên + định dạng -> tạo bản mới, ẩn bản cũ, phục hồi được
    public class FileVersion
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public int VersionNumber { get; set; }
        public string StoragePath { get; set; } = null!;
        public long FileSizeBytes { get; set; }
        public string Format { get; set; } = null!;
        public string? Checksum { get; set; }
        public bool IsHidden { get; set; }
        public Guid? UploadedByAccountId { get; set; }
        public DateTime? UploadedAt { get; set; }

        public string? ViewerUrn { get; set; }
        public string? PreviewStoragePath { get; set; }

        public FileItem FileItem { get; set; } = null!;
    }
}
