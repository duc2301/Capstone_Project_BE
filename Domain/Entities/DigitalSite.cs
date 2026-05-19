using Domain.Common;

namespace Domain.Entities
{
    // Công trường số — bản đồ giải phóng mặt bằng theo tọa độ dự án
    public class DigitalSite : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = null!;
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }
        public string? MapType { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<CaptureStage> Stages { get; set; } = new List<CaptureStage>();
        public ICollection<SiteAnnotation> Annotations { get; set; } = new List<SiteAnnotation>();
    }
}
