using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Profile
{
    // User tự update profile của mình. Role/Status không có ở đây — chỉ admin sửa được qua AccountController.
    // Partial update: field null nghĩa là "không đổi".
    public class UpdateProfileDTO
    {
        [StringLength(100)]
        public string? UserName { get; set; }

        [EmailAddress]
        [StringLength(255)]
        public string? Email { get; set; }
    }
}
