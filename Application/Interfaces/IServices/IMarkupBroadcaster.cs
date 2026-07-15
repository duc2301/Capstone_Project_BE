using Application.DTOs.ResponseDTOs.Markup;

namespace Application.Interfaces.IServices
{
    public interface IMarkupBroadcaster
    {
        Task NoteAddedAsync(Guid fileItemId, FileNoteResponseDTO note);
        Task NoteUpdatedAsync(Guid fileItemId, FileNoteResponseDTO note);
        Task NoteDeletedAsync(Guid fileItemId, Guid noteId);
    }
}
