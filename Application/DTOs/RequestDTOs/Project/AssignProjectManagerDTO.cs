using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Project
{
    // Admin gán 1 account hiện có làm Project Manager cho project.
    // 1 account có thể làm PM nhiều dự án -> chỉ cần AccountId.
    public class AssignProjectManagerDTO
    {
        [Required]
        public Guid AccountId { get; set; }
    }
}
