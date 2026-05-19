using Application.DTOs;
using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    // 5 endpoint REST chuẩn. Controller cụ thể chỉ cần [Route] + ctor.
    // Validation chạy tự động qua global InvalidModelStateResponseFactory.
    [ApiController]
    public abstract class BaseCrudController<TEntity, TCreate, TUpdate, TResponse>
        : ControllerBase
        where TResponse : class, IResponseDto
    {
        protected readonly IGenericService<TEntity, TCreate, TUpdate, TResponse> Service;

        protected BaseCrudController(
            IGenericService<TEntity, TCreate, TUpdate, TResponse> service)
        {
            Service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(ApiResponse.Success("Retrieved successfully", await Service.GetAllAsync()));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await Service.GetByIdAsync(id);
            return item is null
                ? NotFound(ApiResponse.Fail($"Resource with ID {id} not found."))
                : Ok(ApiResponse.Success("Retrieved successfully", item));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TCreate dto)
        {
            var result = await Service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse.Success("Created successfully", result));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TUpdate dto)
        {
            var result = await Service.UpdateAsync(id, dto);
            return Ok(ApiResponse.Success("Updated successfully", result));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await Service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }
    }
}
