using Application.DTOs.ResponseDTOs.ProjectModel;
using Domain.Enum.Project;

namespace Application.DTOs.ResponseDTOs.Project
{
    public class ProjectResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; } = null!;
        public string? ProjectDescription { get; set; }
        public Guid? ManagerAccountId { get; set; }
        public ProjectStatus Status { get; set; }
        public ProjectPhase Phase { get; set; }

        public ProjectLocationResponseDTO? Location { get; set; }
        public List<ProjectModelResponseDTO> Models { get; set; } = new();
    }
}
