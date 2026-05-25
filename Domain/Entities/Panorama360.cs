namespace Domain.Entities
{
    // Ảnh 360 (flycam) gắn tọa độ, thuộc 1 giai đoạn chụp, nguồn từ kho CDE
    public class Panorama360
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
