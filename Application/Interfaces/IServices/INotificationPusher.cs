using Application.DTOs.ResponseDTOs.Notification;

namespace Application.Interfaces.IServices
{
    // Abstraction để NotificationService (Application) push realtime mà không phụ thuộc SignalR/Presentation.
    // Implementation thật nằm ở Capstone_Project/SignalR/SignalRNotificationPusher.cs
    public interface INotificationPusher
    {
        Task PushAsync(Guid accountId, NotificationResponseDTO payload);
    }
}
