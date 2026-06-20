using Application.DTOs.RequestDTOs.Submittal;
using Application.DTOs.ResponseDTOs.Submittal;

namespace Application.Interfaces.IServices
{
    public interface ISubmittalService
    {
        Task<IEnumerable<SubmittalResponseDTO>> GetAllAsync();
        Task<SubmittalResponseDTO?> GetByIdAsync(Guid id);
        Task<SubmittalResponseDTO> CreateAsync(CreateSubmittalDTO dto);
        Task<SubmittalResponseDTO> UpdateAsync(Guid id, UpdateSubmittalDTO dto);
        Task DeleteAsync(Guid id);
    }
}
