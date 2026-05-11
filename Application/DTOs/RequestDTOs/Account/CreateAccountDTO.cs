using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Account
{
    public class CreateAccountDTO
    {
        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = null!;

        [StringLength(50)]
        public string? Role { get; set; }
    }
}
