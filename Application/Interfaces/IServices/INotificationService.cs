using Application.DTOs.RequestDTOs.Notification;
using Application.DTOs.ResponseDTOs.Notification;

namespace Application.Interfaces.IServices
{
    // Dispatcher tạo Notification từ event của các luồng nghiệp vụ
    // (mention trong thảo luận/issue, submittal trình nộp, file approval, invitation...).
    // Khi save xong, đẩy realtime qua INotificationPusher (SignalR).
    public interface INotificationService
    {
        Task NotifyAsync(
            Guid accountId,
            string message,
            string? senderName = null,
            string? linkType = null,
            string? linkId = null);

        Task NotifyManyAsync(
            IEnumerable<Guid> accountIds,
            string message,
            string? senderName = null,
            string? linkType = null,
            string? linkId = null);

        // GET /api/notifications/me — list của user hiện tại (accountId do controller lấy từ JWT)
        Task<NotificationPageDTO> GetMyAsync(Guid accountId, NotificationFilterDTO filter);

        // POST /api/notifications/{id}/read (accountId do controller lấy từ JWT để kiểm tra chính chủ)
        Task MarkReadAsync(Guid notificationId, Guid accountId);

        Task<int> MarkAllReadAsync(Guid accountId);
    }
}
