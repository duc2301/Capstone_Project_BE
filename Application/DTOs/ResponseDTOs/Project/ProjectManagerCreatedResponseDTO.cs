namespace Application.DTOs.ResponseDTOs.Project
{
    // Kết quả tạo PM + gán vô project.
    public class ProjectManagerCreatedResponseDTO
    {
        public Guid ProjectId { get; set; }
        public Guid ManagerAccountId { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
    }
}
