using Application.DTOs.RequestDTOs.Contract;
using Application.DTOs.ResponseDTOs.Contract;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/contracts")]
    public class ContractsController
        : BaseCrudController<Contract, CreateContractDTO, UpdateContractDTO, ContractResponseDTO>
    {
        public ContractsController(
            IGenericService<Contract, CreateContractDTO, UpdateContractDTO, ContractResponseDTO> service)
            : base(service) { }
    }
}
