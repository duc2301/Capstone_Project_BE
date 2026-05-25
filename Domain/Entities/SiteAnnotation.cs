using Domain.Enum.Site;

namespace Domain.Entities
{
    // Ghi chú trên công trường số: điểm cần bay (số lượng), khoảng cách, diện tích
    public class SiteAnnotation
    {
        public Guid Id { get; set; }
        public Guid DigitalSiteId { get; set; }
        public SiteAnnotationType Type { get; set; }
        public string GeometryJson { get; set; } = null!;   // điểm/đường/vùng GeoJSON
        public string? Content { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public DigitalSite DigitalSite { get; set; } = null!;
    }
}
