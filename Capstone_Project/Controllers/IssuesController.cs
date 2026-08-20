using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Issue;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/issues")]
    public class IssuesController : ControllerBase
    {
        private readonly IIssueService _service;

        public IssuesController(IIssueService service)
        {
            _service = service;
        }

        [HttpGet("by-file/{fileItemId:guid}")]
        public async Task<IActionResult> GetByFileItem(Guid fileItemId)
            => Ok(ApiResponse.Success("Retrieved successfully",
                await _service.GetByFileItemAsync(fileItemId, User.GetAccountId())));

        [HttpGet("assigned-to-me")]
        public async Task<IActionResult> GetAssignedToMe([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(ApiResponse.Success("Retrieved successfully",
                await _service.GetAssignedToMeAsync(User.GetAccountId(), page, pageSize)));

        [HttpGet("my-projects")]
        public async Task<IActionResult> GetForMyProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(ApiResponse.Success("Issues retrieved",
                await _service.GetForMyProjectsAsync(User.GetAccountId(), page, pageSize)));

        [HttpGet("by-project/{projectId:guid}")]
        public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
            => Ok(ApiResponse.Success("Retrieved successfully",
                await _service.GetByProjectAsync(projectId, User.GetAccountId(), page, pageSize)));


        [HttpPost("open-file-ids")]
        public async Task<IActionResult> GetOpenIssueFileIds([FromBody] GetOpenIssueFileIdsDTO dto)
            => Ok(ApiResponse.Success("Retrieved successfully",
                await _service.GetOpenIssueFileIdsForAccountAsync(dto.FileItemIds, User.GetAccountId())));

        [HttpGet("assignable-groups/{fileItemId:guid}")]
        public async Task<IActionResult> GetAssignableGroups(Guid fileItemId)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetAssignableGroupsAsync(fileItemId)));

        [HttpGet("assignable-members/{fileItemId:guid}")]
        public async Task<IActionResult> GetAssignableMembers(Guid fileItemId)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetAssignableMembersAsync(fileItemId)));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully",
                await _service.GetByIdAsync(id, User.GetAccountId())));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateIssueDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto, User.GetAccountId())));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIssueDTO dto)
            => Ok(ApiResponse.Success("Updated successfully",
                await _service.UpdateAsync(id, dto, User.GetAccountId())));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id, User.GetAccountId());
            return Ok(ApiResponse.Success("Deleted successfully"));
        }

        [HttpGet("assignments/pending")]
        public async Task<IActionResult> GetPendingAssignments()
            => Ok(ApiResponse.Success("Retrieved successfully",
                await _service.GetPendingAssignmentsForMeAsync(User.GetAccountId())));

        [HttpPut("{id:guid}/assignment")]
        public async Task<IActionResult> Assign(Guid id, [FromBody] AssignIssueDTO dto)
            => Ok(ApiResponse.Success("Issue assigned",
                await _service.AssignAsync(id, dto, User.GetAccountId())));

        [HttpPost("{id:guid}/assignment/accept")]
        public async Task<IActionResult> AcceptAssignment(Guid id)
            => Ok(ApiResponse.Success("Assignment accepted",
                await _service.AcceptAssignmentAsync(id, User.GetAccountId())));

        [HttpPost("{id:guid}/assignment/reject")]
        public async Task<IActionResult> RejectAssignment(Guid id, [FromBody] RejectIssueAssignmentDTO dto)
            => Ok(ApiResponse.Success("Assignment declined",
                await _service.RejectAssignmentAsync(id, dto.Reason, User.GetAccountId())));

        [HttpPost("{id:guid}/resolve")]
        public async Task<IActionResult> Resolve(Guid id)
            => Ok(ApiResponse.Success("Issue resolved", await _service.ResolveAsync(id, User.GetAccountId())));

        [HttpGet("{id:guid}/participants")]
        public async Task<IActionResult> GetParticipants(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetParticipantsAsync(id)));

        [HttpPost("{id:guid}/participants")]
        public async Task<IActionResult> AddParticipant(Guid id, [FromBody] AddIssueParticipantDTO dto)
        {
            await _service.AddParticipantAsync(id, dto.AccountId, User.GetAccountId());
            return Ok(ApiResponse.Success("Participant added"));
        }

        [HttpDelete("{id:guid}/participants/{accountId:guid}")]
        public async Task<IActionResult> RemoveParticipant(Guid id, Guid accountId)
        {
            await _service.RemoveParticipantAsync(id, accountId, User.GetAccountId());
            return Ok(ApiResponse.Success("Participant removed"));
        }

        // Upload anh/tep truc tiep vao issue (khac voi dinh kem file co san trong comment thao luan).
        [HttpPost("{id:guid}/attachments")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20_971_520)]
        [RequestFormLimits(MultipartBodyLengthLimit = 20_971_520)]
        public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("No file provided.", 400);

            await using var stream = file.OpenReadStream();
            var result = await _service.AddAttachmentAsync(id, stream, file.FileName, file.Length, User.GetAccountId());
            return Ok(ApiResponse.Success("Attachment uploaded", result));
        }
    }
}
