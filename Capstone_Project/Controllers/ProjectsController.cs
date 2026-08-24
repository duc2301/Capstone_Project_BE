using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Domain.Entities;
using Domain.Enum.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/projects")]
    public class ProjectsController : ControllerBase
    {
        private const string BundleContentType = "application/zip";
        private const int BundleBufferSize = 81_920;
        private const string SpreadsheetContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const string ImportTemplateFileName = "khoi-tao-du-an-mau.xlsx";
        private const int ImportMaxBytes = 5_242_880;

        private readonly IProjectFlowService _projectFlow;
        private readonly IProjectService _projectService;
        private readonly IProjectFileBundleService _bundle;
        private readonly IAIService _ai;
        private readonly IProjectImportService _import;

        public ProjectsController(
            IProjectFlowService projectFlow,
            IProjectService projectService,
            IProjectFileBundleService bundle,
            IAIService ai,
            IProjectImportService import)
        {
            _projectFlow = projectFlow;
            _projectService = projectService;
            _bundle = bundle;
            _ai = ai;
            _import = import;
        }

        // Khởi tạo nhanh: tải file BEP (PDF/DOCX) -> AI đọc -> trả các field prefill cho stepper.
        // CHỈ parse, không tạo dự án. Việc tạo vẫn qua POST /api/projects.
        [HttpPost("parse-bep")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(52_428_800)]                                   // 50MB
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> ParseBep(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("No file provided.", 400);

            var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
            await using var stream = file.OpenReadStream();
            var result = await _ai.ParseBepAsync(stream, ext, ct);
            return Ok(ApiResponse.Success("BEP parsed", result));
        }


        [HttpGet("import-template")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadImportTemplate(CancellationToken ct)
        {
            var content = await _import.GenerateTemplateAsync(ct);
            return File(content, SpreadsheetContentType, ImportTemplateFileName);
        }

        [HttpPost("import-preview")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(ImportMaxBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = ImportMaxBytes)]
        public async Task<IActionResult> ImportPreview(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("Chưa chọn file.", 400);

            await using var stream = file.OpenReadStream();
            var result = await _import.ParseAsync(stream, ct);
            return Ok(ApiResponse.Success("Project import parsed", result));
        }


        [HttpPost("{id:guid}/image")]
        [Authorize]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6_291_456)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6_291_456)]
        public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("No file provided.", 400);

            await using var stream = file.OpenReadStream();
            var result = await _projectService.SetImageAsync(
                id, stream, file.FileName, file.Length, User.GetAccountId(), User.IsAdmin(), ct);
            return Ok(ApiResponse.Success("Project image updated", result));
        }

        // Admin gán 1 account hiện có làm Project Manager.
        // 1 account có thể làm PM nhiều dự án.
        [HttpPost("{id:guid}/manager")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignManager(Guid id, [FromBody] AssignProjectManagerDTO dto)
        {
            var result = await _projectFlow.AssignManagerAsync(id, dto, User.GetUserName(), User.GetAccountId());
            return Ok(ApiResponse.Success("Manager assigned", result));
        }

        [HttpPost("{id:guid}/participants/bulk")]
        [Authorize]
        public async Task<IActionResult> AddParticipants(Guid id, [FromBody] AddParticipantsBulkDTO dto)
        {
            var result = await _projectFlow.AddParticipantsAsync(id, dto, User.GetAccountId(), User.GetSystemRole());
            return Ok(ApiResponse.Success($"{result.Count} participant(s) added", result));
        }

        [HttpGet("{id:guid}/participants")]
        [Authorize]
        public async Task<IActionResult> GetParticipants(Guid id)
        {
            var result = await _projectFlow.GetParticipantsAsync(id);
            return Ok(ApiResponse.Success("Participants retrieved", result));
        }

        [HttpPut("{id:guid}/participants/{groupId:guid}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateParticipantStatus(
            Guid id, Guid groupId, [FromBody] UpdateParticipantStatusDTO dto)
        {
            var result = await _projectFlow.UpdateParticipantStatusAsync(id, groupId, dto, User.GetAccountId());
            return Ok(ApiResponse.Success("Participant status updated", result));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateProjectDTO dto)
        {
            var result = await _projectService.CreateAsync(dto, User.GetAccountId());
            return Ok(ApiResponse.Success("Project created", result));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] ProjectStatus? status = null,
            [FromQuery] Guid? ownerOrganizationId = null)
        {
            var result = await _projectService.GetAllAsync(page, pageSize, search, status, ownerOrganizationId);
            return Ok(ApiResponse.Success("Projects retrieved", result));
        }

        [HttpGet("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _projectService.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse("Project not found.", 404);
            return Ok(ApiResponse.Success("Project retrieved", result));
        }

        // Dự án người dùng hiện tại đang tham gia (qua group) hoặc làm PM.
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] ProjectStatus? status = null,
            [FromQuery] Guid? ownerOrganizationId = null)
        {
            var result = await _projectFlow.GetMyProjectsPagedAsync(
                User.GetAccountId(), page, pageSize, search, status, ownerOrganizationId);
            return Ok(ApiResponse.Success("My projects retrieved", result));
        }

        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDTO dto)
        {
            var result = await _projectService.UpdateAsync(id, dto, User.GetAccountId(), User.IsAdmin());
            return Ok(ApiResponse.Success("Project updated", result));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _projectService.DeleteAsync(id, User.GetAccountId());
            return Ok(ApiResponse.Success("Project deleted"));
        }

        [HttpGet("{id:guid}/files/bundle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadFileBundle(Guid id, CancellationToken ct)
        {
            var fileName = await _bundle.ResolveBundleFileNameAsync(id, ct);
            var buffer = CreateBundleBuffer();

            try
            {
                await _bundle.WriteBundleAsync(id, User.GetAccountId(), buffer, ct);
                buffer.Position = 0;
            }
            catch
            {
                await buffer.DisposeAsync();
                throw;
            }

            return File(buffer, BundleContentType, fileName);
        }

        private static FileStream CreateBundleBuffer() => new(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            BundleBufferSize,
            FileOptions.DeleteOnClose);
    }
}
