using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Issue;

namespace Application.Interfaces.IServices
{
    public interface IIssueService
    {
        Task<IEnumerable<IssueResponseDTO>> GetAllAsync();
        Task<IssueResponseDTO?> GetByIdAsync(Guid id);
        Task<IssueResponseDTO> CreateAsync(CreateIssueDTO dto);
        Task<IssueResponseDTO> UpdateAsync(Guid id, UpdateIssueDTO dto);
        Task DeleteAsync(Guid id);
    }
}
