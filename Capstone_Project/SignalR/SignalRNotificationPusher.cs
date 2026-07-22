using Application.DTOs.ResponseDTOs.Notification;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    public class SignalRNotificationPusher : INotificationPusher
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRNotificationPusher(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public Task PushAsync(Guid accountId, NotificationResponseDTO payload)
            => _hub.Clients.User(accountId.ToString())
                   .SendAsync(SignalREventNames.ReceiveNotification, payload);
    }
}
