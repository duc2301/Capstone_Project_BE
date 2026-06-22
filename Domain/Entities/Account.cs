using Domain.Enum.Account;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Account
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public AccountRole? Role { get; set; }
        public AccountStatus? Status { get; set; }
        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordTokenExpiresAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
