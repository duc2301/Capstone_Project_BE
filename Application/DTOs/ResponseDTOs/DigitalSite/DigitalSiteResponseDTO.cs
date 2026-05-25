namespace Application.DTOs.ResponseDTOs.DigitalSite
{
    public class DigitalSiteResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = null!;
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }
        public string? MapType { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
