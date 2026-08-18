using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.ContractPackage;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/contract-packages")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class ContractPackagesController : ControllerBase
    {
        private const string RetrievedMessage = "Retrieved successfully";

        private readonly IContractPackageService _service;

        public ContractPackagesController(IContractPackageService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(ApiResponse.Success(RetrievedMessage, await _service.GetAllAsync(page, pageSize)));

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
            => Ok(ApiResponse.Success(RetrievedMessage, await _service.GetMineAsync(User.GetAccountId())));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success(RetrievedMessage, await _service.GetByIdAsync(id)));

        [HttpGet("project/{projectId:guid}")]
        public async Task<IActionResult> GetByProjectId(Guid projectId)
            => Ok(ApiResponse.Success(RetrievedMessage, await _service.GetByProjectIdAsync(projectId)));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateContractPackageDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto, User.GetAccountId())));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractPackageDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto, User.GetAccountId())));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id, User.GetAccountId());
            return Ok(ApiResponse.Success("Deleted successfully"));
        }


    }
}
