using System.ComponentModel.DataAnnotations;
using Domain.Enum.Department;

namespace Application.DTOs.RequestDTOs.Employee
{
    public class CreateEmployeeDTO
    {
        [Required]
        public Guid AccountId { get; set; }

        public Guid? DepartmentId { get; set; }

        [Required]
        public DepartmentRole Role { get; set; }
    }
}
