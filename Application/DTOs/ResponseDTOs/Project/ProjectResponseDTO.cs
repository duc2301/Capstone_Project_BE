using Domain.Enum.Project;

namespace Application.DTOs.ResponseDTOs.Project
{
    public class ProjectResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; } = null!;
        public string? ProjectDescription { get; set; }
        public Guid? ManagerAccountId { get; set; }
        public string? ProjectCode { get; set; }
        public string? ProjectImageUrl { get; set; }
        public ProjectStatus Status { get; set; }

        public Guid? OwnerOrganizationId { get; set; }
        public string? OwnerOrganizationName { get; set; }

        public string? ContactAddress { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ProjectLocationResponseDTO? Location { get; set; }
    }
}
