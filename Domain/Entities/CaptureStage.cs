namespace Domain.Entities
{
    // Giai đoạn chụp (vd mỗi tháng 1 lần) — phải tạo giai đoạn mới up được ảnh 360
    public class CaptureStage
    {
        public Guid Id { get; set; }
        public Guid DigitalSiteId { get; set; }
        public string Name { get; set; } = null!;
        public DateTime? CaptureDate { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedAt { get; set; }

        public DigitalSite DigitalSite { get; set; } = null!;
        public ICollection<Panorama360> Panoramas { get; set; } = new List<Panorama360>();
        public ICollection<SiteImage> Images { get; set; } = new List<SiteImage>();
    }
}
