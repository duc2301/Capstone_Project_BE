using System.ComponentModel.DataAnnotations;
using Domain.Enum.Model;

namespace Application.DTOs.RequestDTOs.ModelFile
{
    public class CreateModelFileDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        public Guid? ProjectModelId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        public Guid? SourceFileVersionId { get; set; }
        public double? OffsetX { get; set; }
        public double? OffsetY { get; set; }
        public double? OffsetZ { get; set; }
        public string? RotationJson { get; set; }

        [Required]
        public ModelFileStatus Status { get; set; }
    }
}
