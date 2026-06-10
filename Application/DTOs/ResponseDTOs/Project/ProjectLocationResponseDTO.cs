namespace Application.DTOs.ResponseDTOs.Project
{
    public class ProjectLocationResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
