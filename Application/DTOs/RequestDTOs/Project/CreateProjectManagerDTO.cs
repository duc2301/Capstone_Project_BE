using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Project
{
    // Admin gọi: tạo account làm Project Manager + gán vào project trong 1 transaction.
    public class CreateProjectManagerDTO
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string UserName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = null!;
    }
}
