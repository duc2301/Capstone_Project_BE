using Application.DTOs.RequestDTOs.Contract;
using Application.DTOs.ResponseDTOs.Contract;

namespace Application.Interfaces.IServices
{
    public interface IContractService
    {
        Task<IEnumerable<ContractResponseDTO>> GetAllAsync();
        Task<ContractResponseDTO?> GetByIdAsync(Guid id);
        Task<ContractResponseDTO> CreateAsync(CreateContractDTO dto);
        Task<ContractResponseDTO> UpdateAsync(Guid id, UpdateContractDTO dto);
        Task DeleteAsync(Guid id);
    }
}
