using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Folder;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/folders")]
    public class FoldersController : ControllerBase
    {
        private readonly IFolderService _service;

        public FoldersController(IFolderService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetAllAsync()));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFolderDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }
    }
}
