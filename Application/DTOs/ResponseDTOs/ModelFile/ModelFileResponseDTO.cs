using Domain.Enum.Model;

namespace Application.DTOs.ResponseDTOs.ModelFile
{
    public class ModelFileResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ProjectModelId { get; set; }
        public string Name { get; set; } = null!;
        public Guid? SourceFileVersionId { get; set; }
        public double? OffsetX { get; set; }
        public double? OffsetY { get; set; }
        public double? OffsetZ { get; set; }
        public string? RotationJson { get; set; }
        public ModelFileStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
