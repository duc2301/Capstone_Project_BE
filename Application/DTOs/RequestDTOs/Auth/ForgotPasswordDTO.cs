using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Auth
{
    public class ForgotPasswordDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
    }
}
