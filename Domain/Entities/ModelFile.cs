using Domain.Enum.Model;

using Domain.Common;

namespace Domain.Entities
{
    // File mô hình IFC lấy từ kho CDE, đặt theo offset/tọa độ trong công trình mẫu
    public class ModelFile : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ProjectModelId { get; set; }
        public string Name { get; set; } = null!;
        public Guid? SourceFileVersionId { get; set; }   // file IFC trong CDE
        public double? OffsetX { get; set; }
        public double? OffsetY { get; set; }
        public double? OffsetZ { get; set; }
        public string? RotationJson { get; set; }
        public ModelFileStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ProjectModel? ProjectModel { get; set; }
        public ICollection<ModelObject> Objects { get; set; } = new List<ModelObject>();
    }
}
