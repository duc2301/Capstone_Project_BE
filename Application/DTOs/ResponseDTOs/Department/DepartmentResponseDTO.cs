using Domain.Enum.Department;

namespace Application.DTOs.ResponseDTOs.Department
{
    public class DepartmentResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Type { get; set; }
    }
}
