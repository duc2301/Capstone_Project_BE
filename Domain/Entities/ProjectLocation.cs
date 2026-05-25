namespace Domain.Entities
{
    // Thông tin địa lý dự án (3 chấm > thông tin địa lý > đặt làm mặc định)
    public class ProjectLocation
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
    }
}
