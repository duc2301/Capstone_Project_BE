using Application.DTOs.ResponseDTOs.Discussion;

namespace Application.Interfaces.IServices
{
    public interface IDiscussionBroadcaster
    {
        Task MessagePostedAsync(Guid fileItemId, DiscussionMessageResponseDTO message);
    }
}
