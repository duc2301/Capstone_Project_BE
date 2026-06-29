using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentIngestService _ingest;

        public DocumentsController(IDocumentIngestService ingest)
        {
            _ingest = ingest;
        }

        [HttpPost("ingest/{fileItemId:guid}")]
        public async Task<IActionResult> Ingest(Guid fileItemId, CancellationToken ct)
        {
            var documentId = await _ingest.IngestFileAsync(fileItemId, ct);
            return Ok(new { documentId });
        }
    }
}
