using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Auth
{
    public class VerifyOtpDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "OTP is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
        public string Otp { get; set; } = null!;
    }
}
