using Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    public class SignalRGroupNotifier : IGroupRealtimeNotifier
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRGroupNotifier(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public Task MemberRoleChangedAsync(Guid accountId, Guid groupId, string newRole)
            => _hub.Clients.User(accountId.ToString()).SendAsync(
                SignalREventNames.GroupMemberRoleChanged, new { groupId, newRole });
    }
}
