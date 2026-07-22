using Domain.Common;
using Domain.Enum.Project;

namespace Domain.Entities
{
    // Project rỗng khi tạo; Manager + bên tham gia (Group) set sau qua endpoint chuyên dụng.
    public class Project : IEntity
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; } = null!;
        public string? ProjectDescription { get; set; }
        public Guid? ManagerAccountId { get; set; }   // Admin gán qua POST /api/projects/{id}/manager
        public string? ProjectCode { get; set; }
        public string? ProjectImageUrl { get; set; }
        public ProjectStatus Status { get; set; }     // Planning/Active/OnHold/Completed/Closed
        public ProjectPhase Phase { get; set; }       // Concept/Design/Construction/Handover/Operation
    }
}
