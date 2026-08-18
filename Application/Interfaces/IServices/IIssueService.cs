using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Issue;

namespace Application.Interfaces.IServices
{
    public interface IIssueService
    {
        Task<IEnumerable<IssueResponseDTO>> GetByFileItemAsync(Guid fileItemId, Guid accountId);

        Task<PagedResult<ProjectIssueListItemDTO>> GetByProjectAsync(Guid projectId, Guid accountId, int page, int pageSize);
        Task<PagedResult<ProjectIssueListItemDTO>> GetForMyProjectsAsync(Guid accountId, int page, int pageSize);
        Task<PagedResult<ProjectIssueListItemDTO>> GetAssignedToMeAsync(Guid accountId, int page, int pageSize);
        Task<IssueResponseDTO?> GetByIdAsync(Guid id, Guid accountId);
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
        Task<IEnumerable<Guid>> GetOpenIssueFileIdsForAccountAsync(IEnumerable<Guid> fileItemIds, Guid accountId);
        Task<IEnumerable<AssignableMemberDTO>> GetAssignableMembersAsync(Guid fileItemId);
        Task<IEnumerable<AssignableGroupDTO>> GetAssignableGroupsAsync(Guid fileItemId);
    }
}
