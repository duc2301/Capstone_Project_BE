using System.ComponentModel.DataAnnotations;
using Domain.Enum.Project;

namespace Application.DTOs.RequestDTOs.Project
{
    // Project RỖNG khi tạo:
    //  - KHÔNG có DepartmentId (bên tham gia add qua POST /api/projects/{id}/participants/bulk)
    //  - KHÔNG có ManagerAccountId (gán qua POST /api/projects/{id}/manager)
    public class CreateProjectDTO
    {
        [Required]
        [StringLength(250)]
        public string ProjectName { get; set; } = null!;

        [StringLength(2000)]
        public string? ProjectDescription { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
        public ProjectPhase Phase { get; set; } = ProjectPhase.Concept;
    }
}
