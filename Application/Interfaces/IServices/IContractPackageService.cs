using Application.DTOs.RequestDTOs.ContractPackage;
using Application.DTOs.ResponseDTOs.ContractPackage;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IContractPackageService
        : IGenericService<ContractPackage, CreateContractPackageDTO, UpdateContractPackageDTO, ContractPackageResponseDTO>
    {
    }
}
