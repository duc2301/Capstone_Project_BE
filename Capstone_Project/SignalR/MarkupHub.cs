using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    [Authorize]
    public class MarkupHub : Hub
    {
        public static string GroupName(string fileItemId) => $"file-markup:{fileItemId}";

        public Task JoinFile(string fileItemId)
            => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(fileItemId));

        public Task LeaveFile(string fileItemId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(fileItemId));
    }
}
