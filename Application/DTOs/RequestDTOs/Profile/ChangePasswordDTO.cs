using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Profile
{
    public class ChangePasswordDTO
    {
        [Required]
        public string CurrentPassword { get; set; } = null!;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = null!;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "New passwords do not match.")]
        public string ConfirmNewPassword { get; set; } = null!;
    }
}
