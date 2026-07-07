using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Auth
{
    public class ResendOtpDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = null!;
    }
}
