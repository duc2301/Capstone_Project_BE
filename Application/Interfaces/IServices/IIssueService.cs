using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Issue;

namespace Application.Interfaces.IServices
{
    public interface IIssueService
    {
        Task<IEnumerable<IssueResponseDTO>> GetAllAsync();
        Task<IEnumerable<IssueResponseDTO>> GetByFileItemAsync(Guid fileItemId);
        Task<IssueResponseDTO?> GetByIdAsync(Guid id);
        Task<IssueResponseDTO> CreateAsync(CreateIssueDTO dto, Guid actorId);
        Task<IssueResponseDTO> UpdateAsync(Guid id, UpdateIssueDTO dto);
        Task DeleteAsync(Guid id);

        /// <summary>Danh dau issue la "Da giai quyet" (map sang IssueStatus.Closed, khong can sua file).</summary>
        Task<IssueResponseDTO> ResolveAsync(Guid issueId, Guid actorId);

        Task<IEnumerable<Guid>> GetParticipantsAsync(Guid issueId);
        Task AddParticipantAsync(Guid issueId, Guid accountId, Guid actorId);
        Task RemoveParticipantAsync(Guid issueId, Guid accountId, Guid actorId);
        Task<IssueAttachmentResponseDTO> AddAttachmentAsync(
            Guid issueId, Stream content, string fileName, long fileSizeBytes, Guid actorId);
        Task<IEnumerable<Guid>> GetOpenIssueFileIdsAsync(IEnumerable<Guid> fileItemIds);
        Task<IEnumerable<AssignableMemberDTO>> GetAssignableMembersAsync(Guid fileItemId);
    }
}
