using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    /// <summary>
    /// Gửi email onboarding cho tài khoản vừa import Excel (out-of-band).
    /// Tiêu thụ IAccountEmailQueue. Khi khởi động, requeue các tài khoản còn
    /// IsOnboardingEmailPending == true (durability: không mất email nếu app restart giữa chừng).
    /// </summary>
    public sealed class AccountEmailWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAccountEmailQueue _queue;
        private readonly ILogger<AccountEmailWorker> _logger;

        public AccountEmailWorker(
            IServiceScopeFactory scopeFactory,
            IAccountEmailQueue queue,
            ILogger<AccountEmailWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RequeuePendingAsync(stoppingToken);

            try
            {
                await foreach (var job in _queue.ReadAllAsync(stoppingToken))
                {
                    await ProcessAsync(job, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
        }

        private async Task ProcessAsync(AccountEmailJob job, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var account = await uow.AccountRepository.GetByIdAsync(job.AccountId);
                if (account == null || !account.IsOnboardingEmailPending)
                    return; // Đã gửi hoặc không còn tồn tại -> bỏ qua (idempotent).

                var frontendBaseUrl = configuration["FrontendDeployBaseUrl"]!.TrimEnd('/');
                // welcome=1 để trang /reset-password hiển thị nội dung "chào mừng / đặt mật khẩu lần đầu"
                // thay vì "đặt lại mật khẩu" (phân biệt luồng onboarding với forgot-password).
                var setPasswordUrl =
                    $"{frontendBaseUrl}/reset-password?email={Uri.EscapeDataString(account.Email)}&token={account.ResetPasswordToken}&welcome=1";

                // Password chỉ có khi job đến trực tiếp từ import. Nếu job được requeue lúc app khởi động lại
                // thì plaintext đã mất (không lưu DB) -> bỏ dòng mật khẩu, user dùng nút "Đặt mật khẩu".
                var passwordLine = string.IsNullOrEmpty(job.Password)
                    ? string.Empty
                    : $"Mật khẩu đăng nhập: {job.Password}\n";

                var body =
                    $"Xin chào {account.UserName},\n\n" +
                    "Quản trị viên đã tạo tài khoản cho bạn trên BIM CDE Portal.\n\n" +
                    $"Email đăng nhập: {account.Email}\n" +
                    passwordLine + "\n" +
                    "Bạn có thể đăng nhập ngay bằng thông tin trên. Nếu muốn đổi sang mật khẩu " +
                    "riêng, hãy dùng nút bên dưới (không bắt buộc, liên kết không yêu cầu đăng nhập).";

                await emailService.SendEmailAsync(
                    account.Email,
                    "Tài khoản của bạn đã được tạo - BIM CDE Portal",
                    body,
                    new EmailAction("Đặt mật khẩu", setPasswordUrl));

                account.IsOnboardingEmailPending = false;
                account.UpdatedAt = DateTime.UtcNow;
                await uow.CommitAsync();

                _logger.LogInformation("Sent onboarding email to {Email}.", account.Email);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                // Giữ IsOnboardingEmailPending == true -> sẽ được requeue ở lần khởi động sau.
                _logger.LogError(ex, "Failed to send onboarding email for Account {Id}.", job.AccountId);
            }
        }

        private async Task RequeuePendingAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var pending = (await uow.AccountRepository
                    .FindAsync(a => a.IsOnboardingEmailPending)).ToList();

                // Không có plaintext mật khẩu ở đây (chỉ lưu hash) -> email sẽ chỉ đưa link đặt mật khẩu.
                foreach (var account in pending)
                    _queue.Enqueue(account.Id, null);

                if (pending.Count > 0)
                    _logger.LogInformation("Re-enqueued {Count} pending onboarding email(s) on startup.", pending.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-enqueue pending onboarding emails on startup.");
            }
        }
    }
}
