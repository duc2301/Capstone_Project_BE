using System.ComponentModel.DataAnnotations;
using Domain.Enum.Model;

namespace Application.DTOs.RequestDTOs.ModelFile
{
    public class UpdateModelFileDTO
    {
        public Guid? ProjectModelId { get; set; }

        [StringLength(250)]
        public string? Name { get; set; }

        public Guid? SourceFileVersionId { get; set; }
        public double? OffsetX { get; set; }
        public double? OffsetY { get; set; }
        public double? OffsetZ { get; set; }
        public string? RotationJson { get; set; }
        public ModelFileStatus? Status { get; set; }
    }
}
