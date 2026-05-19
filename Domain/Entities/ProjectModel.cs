using Domain.Common;

namespace Domain.Entities
{
    // Công trình mẫu (federated) — gắn nhiều ModelFile theo tọa độ GIS thực tế
    public class ProjectModel : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<ModelFile> ModelFiles { get; set; } = new List<ModelFile>();
    }
}
