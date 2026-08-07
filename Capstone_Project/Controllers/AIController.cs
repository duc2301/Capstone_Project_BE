using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/ai")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AIController : ControllerBase
    {
        private readonly IAIService _ai;

        public AIController(IAIService ai)
        {
            _ai = ai;
        }

        // Test: phân tích nội dung file = tóm tắt + cờ nghi ngờ (bình thường worker tự chạy sau upload).
        // GET /api/ai/analyze/{fileItemId}
        [HttpGet("analyze/{fileItemId:guid}")]
        public async Task<IActionResult> Analyze(Guid fileItemId, CancellationToken ct)
        {
            var result = await _ai.AnalyzeContentAsync(fileItemId, ct);
            return Ok(result);
        }
    }
}
