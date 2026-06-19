using Application.DTOs.RequestDTOs.Discussion;
using Application.DTOs.ResponseDTOs.Discussion;

namespace Application.Interfaces.IServices
{
    public interface IDiscussionService
    {
        Task<IEnumerable<DiscussionResponseDTO>> GetAllAsync();
        Task<DiscussionResponseDTO?> GetByIdAsync(Guid id);
        Task<DiscussionResponseDTO> CreateAsync(CreateDiscussionDTO dto);
        Task<DiscussionResponseDTO> UpdateAsync(Guid id, UpdateDiscussionDTO dto);
        Task DeleteAsync(Guid id);
    }
}
