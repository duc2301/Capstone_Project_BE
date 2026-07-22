using Application.DTOs.ResponseDTOs.Markup;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    public class SignalRMarkupBroadcaster : IMarkupBroadcaster
    {
        private readonly IHubContext<MarkupHub> _hub;

        public SignalRMarkupBroadcaster(IHubContext<MarkupHub> hub)
        {
            _hub = hub;
        }

        public Task NoteAddedAsync(Guid fileItemId, FileNoteResponseDTO note)
            => _hub.Clients.Group(MarkupHub.GroupName(fileItemId.ToString()))
                   .SendAsync(SignalREventNames.MarkupNoteAdded, note);

        public Task NoteUpdatedAsync(Guid fileItemId, FileNoteResponseDTO note)
            => _hub.Clients.Group(MarkupHub.GroupName(fileItemId.ToString()))
                   .SendAsync(SignalREventNames.MarkupNoteUpdated, note);

        public Task NoteDeletedAsync(Guid fileItemId, Guid noteId)
            => _hub.Clients.Group(MarkupHub.GroupName(fileItemId.ToString()))
                   .SendAsync(SignalREventNames.MarkupNoteDeleted, new { fileItemId, noteId });
    }
}
