namespace Application.Interfaces.IServices
{
    // Dispatcher tạo Notification từ event của các luồng nghiệp vụ
    // (mention trong thảo luận/issue, submittal trình nộp, file approval...).
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
    }
}
