using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Markup;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/markups")]
    [Authorize]
    public class MarkupsController : ControllerBase
    {
        private readonly IMarkupService _service;

        public MarkupsController(IMarkupService service)
        {
            _service = service;
        }

        [HttpPost("sets")]
        public async Task<IActionResult> CreateSet([FromBody] CreateMarkupSetDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup set created", await _service.CreateSetAsync(dto, User.GetAccountId(), ct)));

        [HttpGet("sets/by-file/{fileItemId:guid}")]
        public async Task<IActionResult> GetByFile(Guid fileItemId, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup sets retrieved", await _service.GetSetsByFileAsync(fileItemId, User.GetAccountId(), ct)));

        [HttpGet("sets/by-issue/{issueId:guid}")]
        public async Task<IActionResult> GetByIssue(Guid issueId, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup sets retrieved", await _service.GetSetsByIssueAsync(issueId, User.GetAccountId(), ct)));

        [HttpGet("sets/{setId:guid}")]
        public async Task<IActionResult> GetSetDetail(Guid setId, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup set retrieved", await _service.GetSetDetailAsync(setId, User.GetAccountId(), ct)));

        [HttpPost("sets/{setId:guid}/status")]
        public async Task<IActionResult> UpdateSetStatus(Guid setId, [FromBody] UpdateMarkupSetStatusDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup set status updated", await _service.UpdateSetStatusAsync(setId, dto.Status, User.GetAccountId(), ct)));

        [HttpPost("sets/{setId:guid}/issue")]
        public async Task<IActionResult> LinkToIssue(Guid setId, [FromBody] LinkMarkupToIssueDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup set issue link updated", await _service.LinkToIssueAsync(setId, dto.IssueId, User.GetAccountId(), ct)));

        [HttpPost("sets/{setId:guid}/notes")]
        public async Task<IActionResult> AddNote(Guid setId, [FromBody] CreateFileNoteDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup note added", await _service.AddNoteAsync(setId, dto, User.GetAccountId(), ct)));

        [HttpPut("notes/{noteId:guid}")]
        public async Task<IActionResult> UpdateNote(Guid noteId, [FromBody] UpdateFileNoteDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("Markup note updated", await _service.UpdateNoteAsync(noteId, dto, User.GetAccountId(), ct)));

        [HttpDelete("notes/{noteId:guid}")]
        public async Task<IActionResult> DeleteNote(Guid noteId, CancellationToken ct)
        {
            await _service.DeleteNoteAsync(noteId, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("Markup note deleted"));
        }
    }
}
