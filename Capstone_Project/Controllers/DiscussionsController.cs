using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Discussion;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/discussions")]
    public class DiscussionsController : ControllerBase
    {
        private readonly IDiscussionService _service;

        public DiscussionsController(IDiscussionService service)
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
        public async Task<IActionResult> Create([FromBody] CreateDiscussionDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDiscussionDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }

        [HttpGet("{id:guid}/messages")]
        public async Task<IActionResult> GetMessages(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetMessagesAsync(id)));

        [HttpPost("{id:guid}/messages")]
        public async Task<IActionResult> PostMessage(Guid id, [FromBody] PostDiscussionMessageDTO dto)
            => Ok(ApiResponse.Success("Message posted", await _service.PostMessageAsync(id, dto, User.GetAccountId())));
    }
}
