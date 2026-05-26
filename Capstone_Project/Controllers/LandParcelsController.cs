using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.LandParcel;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/land-parcels")]
    public class LandParcelsController : ControllerBase
    {
        private readonly ILandParcelService _service;

        public LandParcelsController(ILandParcelService service)
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
        public async Task<IActionResult> Create([FromBody] CreateLandParcelDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLandParcelDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }
    }
}
