using Domain.Enum.Department;

namespace Application.DTOs.ResponseDTOs.Employee
{
    public class EmployeeResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid? DepartmentId { get; set; }
        public DepartmentRole Role { get; set; }
    }
}
