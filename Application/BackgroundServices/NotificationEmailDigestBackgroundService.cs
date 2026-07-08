using Application.Interfaces.IServices;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    /// <summary>
    /// Background Service chạy ngầm, mỗi n phút quét Notification:
    ///   - Chưa đọc (IsRead == false)
    ///   - Chưa gửi email (IsEmailSent == false)
    ///   - Đã tồn tại qua khoảng thời gian chờ (delay) kể từ lúc tạo
    /// Gom nhóm theo AccountId -> gửi 1 email digest duy nhất cho mỗi user.
    /// Sau khi gửi email thành công -> đánh dấu IsEmailSent = true.
    /// </summary>
    public class NotificationEmailDigestBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationEmailDigestBackgroundService> _logger;
        private readonly TimeSpan _pollingInterval;
        private readonly TimeSpan _notificationDelay;

        public NotificationEmailDigestBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationEmailDigestBackgroundService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            // Đọc cấu hình từ appsettings.json, mặc định 3 phút poll, 3 phút delay
            var pollingMinutes = configuration.GetValue("NotificationDigest:PollingIntervalMinutes", 3);
            var delayMinutes = configuration.GetValue("NotificationDigest:DelayMinutes", 3);
            _pollingInterval = TimeSpan.FromMinutes(pollingMinutes);
            _notificationDelay = TimeSpan.FromMinutes(delayMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "NotificationEmailDigestBackgroundService started. Polling every {Interval} minutes, delay {Delay} minutes.",
                _pollingInterval.TotalMinutes, _notificationDelay.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingNotificationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing notification email digest.");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }
        }

        private async Task ProcessPendingNotificationsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CDESystemDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var frontendBaseUrl = configuration["FrontendLocalBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

            var cutoff = DateTime.UtcNow.Subtract(_notificationDelay);

            //lấy tất cả notification chưa đọc, chưa gửi email, đã qua delay
            var pendingNotifications = await dbContext.Notifications
                .Include(n => n.Account)
                .Where(n => !n.IsRead && !n.IsEmailSent && n.SendAt <= cutoff)
                .Where(n => n.Account != null && n.Account.IsEmailVerified) // Chỉ gửi cho user đã xác thực email
                .OrderBy(n => n.SendAt)
                .ToListAsync(ct);

            if (pendingNotifications.Count == 0)
                return;

            // Gom nhóm theo AccountId
            var grouped = pendingNotifications.GroupBy(n => n.AccountId);

            foreach (var group in grouped)
            {
                var account = group.First().Account;
                if (account == null || string.IsNullOrWhiteSpace(account.Email))
                    continue;

                var notifications = group.ToList();

                try
                {
                    var emailBody = BuildDigestEmailBody(account.UserName, notifications, frontendBaseUrl);
                    var subject = notifications.Count == 1
                        ? $"🔔 Bạn có 1 thông báo mới trên BIM CDE Portal"
                        : $"🔔 Bạn có {notifications.Count} thông báo mới trên BIM CDE Portal";

                    await emailService.SendEmailAsync(account.Email, subject, emailBody);

                    // Đánh dấu đã gửi email (Bulk Update)
                    foreach (var noti in notifications)
                        noti.IsEmailSent = true;

                    _logger.LogInformation(
                        "Sent digest email to {Email} with {Count} notification(s).",
                        account.Email, notifications.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send digest email to {Email}. Will retry next cycle.",
                        account.Email);
                    // Không đánh dấu IsEmailSent -> sẽ retry lần sau
                }
            }

            // Commit tất cả thay đổi IsEmailSent trong 1 lần
            await dbContext.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Tạo nội dung email digest dạng plain text (GmailEmailService sẽ wrap thành HTML).
        /// </summary>
        private static string BuildDigestEmailBody(
            string userName,
            List<Notification> notifications,
            string frontendBaseUrl)
        {
            var lines = new List<string>
            {
                $"Xin chào {userName},",
                "",
                $"Bạn có {notifications.Count} thông báo mới chưa đọc trên BIM CDE Portal:",
                ""
            };

            for (var i = 0; i < notifications.Count; i++)
            {
                var n = notifications[i];
                var time = n.SendAt.AddHours(7).ToString("dd/MM/yyyy HH:mm"); // UTC+7
                lines.Add($"{i + 1}. {n.Message} (lúc {time})");
            }

            lines.Add("");
            lines.Add($"Truy cập hệ thống để xem chi tiết: {frontendBaseUrl}/notifications");
            lines.Add("");
            lines.Add("Trân trọng,");
            lines.Add("BIM CDE Portal");

            return string.Join("\n", lines);
        }
    }
}
