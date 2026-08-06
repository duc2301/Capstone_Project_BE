using Application.DTOs.RequestDTOs.Contract;
using Application.DTOs.ResponseDTOs.Contract;

namespace Application.Interfaces.IServices
{
    public interface IContractService
    {
        Task<ContractResponseDTO?> GetByIdAsync(Guid id);
        Task<ContractResponseDTO> CreateAsync(CreateContractDTO dto, Guid actorId);
        Task<ContractResponseDTO> UpdateAsync(Guid id, UpdateContractDTO dto, Guid actorId);
        Task DeleteAsync(Guid id, Guid actorId);
    }
}
