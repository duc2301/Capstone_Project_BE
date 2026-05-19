namespace Domain.Entities
{
    // Ảnh thường gắn tọa độ trong 1 giai đoạn chụp
    public class SiteImage
    {
        public Guid Id { get; set; }
        public Guid CaptureStageId { get; set; }
        public Guid? SourceFileVersionId { get; set; }
        public string? ImageStoragePath { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? CapturedAt { get; set; }
        public DateTime? CreatedAt { get; set; }

        public CaptureStage CaptureStage { get; set; } = null!;
    }
}
