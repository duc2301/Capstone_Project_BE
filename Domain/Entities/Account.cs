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

        public Guid? OrganizationId { get; set; }
        public string? AvatarStoragePath { get; set; }


        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordTokenExpiresAt { get; set; }

        // OTP xác thực email khi đăng ký
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailOtp { get; set; }
        public DateTime? EmailOtpExpiresAt { get; set; }

        // Import Excel: đánh dấu tài khoản vừa import đang chờ gửi email onboarding.
        // AccountEmailWorker gửi email rồi set lại false; dùng để requeue khi app khởi động lại (durability).
        public bool IsOnboardingEmailPending { get; set; } = false;

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Organization? Organization { get; set; }
    }
}
