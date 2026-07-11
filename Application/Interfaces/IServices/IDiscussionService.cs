using Application.DTOs.RequestDTOs.Discussion;
using Application.DTOs.ResponseDTOs.Discussion;
using Domain.Enum.Discussion;

namespace Application.Interfaces.IServices
{
    public interface IDiscussionService
    {
        Task<IEnumerable<DiscussionResponseDTO>> GetAllAsync();
        Task<DiscussionResponseDTO?> GetByIdAsync(Guid id);
        Task<DiscussionResponseDTO> CreateAsync(CreateDiscussionDTO dto);
        Task<DiscussionResponseDTO> UpdateAsync(Guid id, UpdateDiscussionDTO dto);
        Task DeleteAsync(Guid id);

        /// <summary>Tao 1 Discussion moi gan voi 1 doi tuong khac (vd Issue) — dung cho luong tu dong tao thread khi tao Issue.</summary>
        Task<DiscussionResponseDTO> CreateForScopeAsync(DiscussionScopeType scopeType, Guid scopeId, Guid projectId, string title, Guid actorId);

        /// <summary>Lay Discussion gan voi 1 doi tuong (vd scopeType=Issue, scopeId=issueId). Null neu chua co.</summary>
        Task<DiscussionResponseDTO?> GetByScopeAsync(DiscussionScopeType scopeType, Guid scopeId);

        Task<IEnumerable<DiscussionMessageResponseDTO>> GetMessagesAsync(Guid discussionId);
        Task<DiscussionMessageResponseDTO> PostMessageAsync(Guid discussionId, PostDiscussionMessageDTO dto, Guid actorId);
    }
}
