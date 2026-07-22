using Application.DTOs.ResponseDTOs.Approval;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    public class SignalRApprovalNotifier : IApprovalRealtimeNotifier
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRApprovalNotifier(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public Task ApprovalChangedAsync(Guid accountId, ApprovalRequestResponseDTO approval)
            => _hub.Clients.User(accountId.ToString()).SendAsync("ApprovalChanged", approval);
    }
}
