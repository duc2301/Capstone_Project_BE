using Domain.Enum.Department;

namespace Application.DTOs.RequestDTOs.Employee
{
    public class UpdateEmployeeDTO
    {
        public Guid? AccountId { get; set; }
        public Guid? DepartmentId { get; set; }
        public DepartmentRole? Role { get; set; }
    }
}
