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

        /// <summary>Upload 1 file/anh (moi, khong phai file co san trong cay thu muc) gan truc tiep vao issue.</summary>
        Task<IssueAttachmentResponseDTO> AddAttachmentAsync(
            Guid issueId, Stream content, string fileName, long fileSizeBytes, Guid actorId);

        /// <summary>Loc trong danh sach fileItemId truyen vao, tra ve nhung file dang co it nhat 1 Issue
        /// chua Closed — dung de FE tu ghep co "Dang xu ly issue" vao danh sach file o cac trang khac
        /// (vd bang danh sach file cua FolderTreeService) ma khong can BE cho do phai biet ve Issue.</summary>
        Task<IEnumerable<Guid>> GetOpenIssueFileIdsAsync(IEnumerable<Guid> fileItemIds);
    }
}
