using Application.DTOs.RequestDTOs.ContractPackage;
using Application.DTOs.ResponseDTOs.ContractPackage;

namespace Application.Interfaces.IServices
{
    public interface IContractPackageService
    {
        Task<IEnumerable<ContractPackageResponseDTO>> GetAllAsync();
        Task<IEnumerable<ContractPackageResponseDTO>> GetByProjectIdAsync(Guid projectId);
        Task<ContractPackageResponseDTO?> GetByIdAsync(Guid id);
        Task<ContractPackageResponseDTO> CreateAsync(CreateContractPackageDTO dto);
        Task<ContractPackageResponseDTO> UpdateAsync(Guid id, UpdateContractPackageDTO dto);
        Task DeleteAsync(Guid id);
        
    }
}
