using Application.DTOs.ApiResponseDTO;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentIngestService _ingest;
        private readonly ISemanticSearchService _search;
        private readonly IProjectFlowService _projectFlow;

        public DocumentsController(IDocumentIngestService ingest, ISemanticSearchService search, IProjectFlowService projectFlow)
        {
            _ingest = ingest;
            _search = search;
            _projectFlow = projectFlow;
        }

        [HttpPost("ingest/{fileItemId:guid}")]
        public async Task<IActionResult> Ingest(Guid fileItemId, CancellationToken ct)
        {
            var documentId = await _ingest.IngestFileAsync(fileItemId, ct);
            return Ok(new { documentId });
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
        [FromQuery] Guid projectId, [FromQuery] string query, CancellationToken ct)
        {
            var accountId = User.GetAccountId();           // ném 401 nếu chưa đăng nhập

            // check user thuộc dự án (Admin bỏ qua)
            if (!User.IsAdmin())
            {
                var myProjects = await _projectFlow.GetMyProjectsAsync(accountId);
                if (!myProjects.Any(p => p.Id == projectId))
                    throw new ApiExceptionResponse("Bạn không thuộc dự án này.", 403);
            }

            var results = await _search.SearchAsync(projectId, query, ct);
            return Ok(ApiResponse.Success("Tìm kiếm thành công", results));
        }
    }
}
