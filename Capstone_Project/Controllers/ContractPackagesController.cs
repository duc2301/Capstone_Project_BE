using Application.DTOs.RequestDTOs.ContractPackage;
using Application.DTOs.ResponseDTOs.ContractPackage;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/contract-packages")]
    public class ContractPackagesController
        : BaseCrudController<ContractPackage, CreateContractPackageDTO, UpdateContractPackageDTO, ContractPackageResponseDTO>
    {
        public ContractPackagesController(
            IGenericService<ContractPackage, CreateContractPackageDTO, UpdateContractPackageDTO, ContractPackageResponseDTO> service)
            : base(service) { }
    }
}
