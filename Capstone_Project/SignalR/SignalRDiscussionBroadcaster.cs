using Application.DTOs.ResponseDTOs.Discussion;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    public class SignalRDiscussionBroadcaster : IDiscussionBroadcaster
    {
        private readonly IHubContext<MarkupHub> _hub;

        public SignalRDiscussionBroadcaster(IHubContext<MarkupHub> hub)
        {
            _hub = hub;
        }

        public Task MessagePostedAsync(Guid fileItemId, DiscussionMessageResponseDTO message)
            => _hub.Clients.Group(MarkupHub.GroupName(fileItemId.ToString()))
                   .SendAsync("DiscussionMessagePosted", message);
    }
}
