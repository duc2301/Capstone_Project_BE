using Application.DTOs.RequestDTOs.Contract;
using Application.DTOs.ResponseDTOs.Contract;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IContractService
        : IGenericService<Contract, CreateContractDTO, UpdateContractDTO, ContractResponseDTO>
    {
    }
}
