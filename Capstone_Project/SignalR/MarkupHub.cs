using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    [Authorize]
    public class MarkupHub : Hub
    {
        private readonly IMarkupService _markupService;

        public MarkupHub(IMarkupService markupService)
        {
            _markupService = markupService;
        }

        public static string GroupName(string fileItemId) => $"file-markup:{fileItemId}";

        public async Task JoinFile(string fileItemId)
        {
            if (!Guid.TryParse(fileItemId, out var id))
                throw new HubException("Invalid file id.");

            var accountId = Context.User?.GetAccountIdOrNull()
                ?? throw new HubException("Authentication required.");
            var isAdmin = Context.User?.IsAdmin() ?? false;

            if (!await _markupService.CanAccessFileMarkupAsync(id, accountId, isAdmin, Context.ConnectionAborted))
                throw new HubException("You do not have permission to view this file.");

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(fileItemId));
        }

        public Task LeaveFile(string fileItemId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(fileItemId));
    }
}
