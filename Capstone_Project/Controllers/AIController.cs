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

        // Test: tóm tắt nội dung file (bình thường worker tự chạy sau upload).
        // GET /api/ai/summarize/{fileItemId}
        [HttpGet("summarize/{fileItemId:guid}")]
        public async Task<IActionResult> Summarize(Guid fileItemId, CancellationToken ct)
        {
            var result = await _ai.SummarizeContentAsync(fileItemId, ct);
            return Ok(result);
        }
    }
}
