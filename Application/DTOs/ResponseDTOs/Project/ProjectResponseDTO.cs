namespace Application.DTOs.ResponseDTOs.Project
{
    public class ProjectResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; } = null!;
        public string ProjectDescription { get; set; } = null!;
        public Guid DepartmentId { get; set; }
        public Guid? ManagerAccountId { get; set; }
    }
}
