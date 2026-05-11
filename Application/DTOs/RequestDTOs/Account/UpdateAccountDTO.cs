using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Account
{
    public class UpdateAccountDTO
    {
        [StringLength(100)]
        public string? UserName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(50)]
        public string? Role { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }
    }
}
