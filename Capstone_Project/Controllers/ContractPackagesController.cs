using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.ContractPackage;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Capstone_Project.Controllers
{
    [Route("api/contract-packages")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class ContractPackagesController : ControllerBase
    {
        private readonly IContractPackageService _service;

        public ContractPackagesController(IContractPackageService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetAllAsync()));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));

        [HttpGet("project/{projectId:guid}")]
        public async Task<IActionResult> GetByProjectId(Guid projectId)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByProjectIdAsync(projectId)));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateContractPackageDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractPackageDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }


    }
}
