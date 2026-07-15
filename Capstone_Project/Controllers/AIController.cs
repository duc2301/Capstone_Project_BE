using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/ai")]
    [ApiController]
    public class AIController : ControllerBase
    {
        private readonly IAIService _ai;

        public AIController(IAIService ai)
        {
            _ai = ai;
        }

        // Test: kiểm tra tên file có khớp nội dung file không.
        // GET /api/ai/check-name/{fileItemId}
        [HttpGet("check-name/{fileItemId:guid}")]
        public async Task<IActionResult> CheckName(Guid fileItemId, CancellationToken ct)
        {
            var result = await _ai.CheckNameMatchesContentAsync(fileItemId, ct);
            return Ok(result);
        }
    }
}
