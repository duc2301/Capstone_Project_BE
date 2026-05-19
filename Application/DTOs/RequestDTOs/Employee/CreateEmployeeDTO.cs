using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Employee
{
    public class CreateEmployeeDTO
    {
        [Required]
        public Guid AccountId { get; set; }

        public Guid? DepartmentId { get; set; }
    }
}
