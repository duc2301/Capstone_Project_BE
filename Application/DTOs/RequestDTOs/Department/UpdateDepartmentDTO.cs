using System.ComponentModel.DataAnnotations;
using Domain.Enum.Department;

namespace Application.DTOs.RequestDTOs.Department
{
    public class UpdateDepartmentDTO
    {
        [StringLength(150)]
        public string? Name { get; set; }

        public DepartmentType? Type { get; set; }
    }
}
