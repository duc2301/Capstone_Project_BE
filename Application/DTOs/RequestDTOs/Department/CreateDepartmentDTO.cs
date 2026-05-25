using System.ComponentModel.DataAnnotations;
using Domain.Enum.Department;

namespace Application.DTOs.RequestDTOs.Department
{
    public class CreateDepartmentDTO
    {
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = null!;

        [Required]
        public string Type { get; set; }
    }
}
