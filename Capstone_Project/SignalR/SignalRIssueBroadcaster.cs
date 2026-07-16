using Application.DTOs.ResponseDTOs.Issue;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    public class SignalRIssueBroadcaster : IIssueBroadcaster
    {
        private readonly IHubContext<MarkupHub> _hub;

        public SignalRIssueBroadcaster(IHubContext<MarkupHub> hub)
        {
            _hub = hub;
        }

        public Task IssueCreatedAsync(Guid fileItemId, IssueResponseDTO issue)
            => _hub.Clients.Group(MarkupHub.GroupName(fileItemId.ToString()))
                   .SendAsync("IssueCreated", issue);

        public Task IssueUpdatedAsync(Guid fileItemId, IssueResponseDTO issue)
            => _hub.Clients.Group(MarkupHub.GroupName(fileItemId.ToString()))
                   .SendAsync("IssueUpdated", issue);
    }
}
