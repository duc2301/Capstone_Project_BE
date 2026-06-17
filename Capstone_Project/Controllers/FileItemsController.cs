using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/file-items")]
    [Authorize]
    public class FileItemsController : ControllerBase
    {
        private readonly IFileItemService _service;
        private readonly IFileUploadService _upload;
        private readonly IApprovalService _approval;

        public FileItemsController(IFileItemService service, IFileUploadService upload, IApprovalService approval)
        {
            _service = service;
            _upload = upload;
            _approval = approval;
        }

        // Luồng tải file lên (multipart/form-data): file + FolderId + FileType + (Name tùy chọn).
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadFileDTO dto, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("No file provided.", 400);

            await using var stream = file.OpenReadStream();
            var result = await _upload.UploadAsync(dto, stream, file.FileName, ct);
            return Ok(ApiResponse.Success("Uploaded successfully", result));
        }

        // Tải file về (kiểm tra quyền Download trong service).
        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id, CancellationToken ct)
        {
            var dl = await _upload.OpenDownloadAsync(id, ct);
            return File(dl.Content, dl.ContentType, dl.FileName);
        }

        /// <summary>
        /// Gửi file CDE để chờ Team Leader phê duyệt.
        /// </summary>
        /// <remarks>
        /// Chỉ member active trong team/project của file mới được gửi duyệt.
        /// </remarks>
        [HttpPost("{id:guid}/submit-approval")]
        public async Task<IActionResult> SubmitApproval(Guid id)
            => Ok(ApiResponse.Success("File submitted for approval", await _approval.SubmitAsync(id)));

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetAllAsync()));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFileItemDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFileItemDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }
    }
}
