using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    // CHỈ DÙNG CHO DEV/TEST: kiểm tra logic File Versioning độc lập với luồng upload.
    // Sẽ gỡ bỏ khi Upload/Zone transition đã tích hợp IFileVersionService.
    [Route("api/file-versions")]
    [Authorize]
    public class FileVersionController : ControllerBase
    {
        private readonly IFileVersionService _fileVersionService;

        public FileVersionController(IFileVersionService fileVersionService)
        {
            _fileVersionService = fileVersionService;
        }

        // Mô phỏng upload vào folder: tài liệu mới -> P01.01 (chưa lưu state);
        // trùng tên -> Working Version +1 (đã lưu state).
        [HttpPost("next-upload")]
        public async Task<IActionResult> GetNextUploadVersion([FromQuery] Guid folderId, [FromQuery] string fileName)
        {
            try
            {
                var result = await _fileVersionService.GetNextUploadVersionAsync(folderId, fileName);
                return Ok(ApiResponse.Success("Next upload version calculated", result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        // Chốt version đầu tiên (P01.01) cho FileItem đã tồn tại.
        [HttpPost("{fileItemId:guid}/initial")]
        public async Task<IActionResult> CreateInitialVersion(Guid fileItemId)
        {
            try
            {
                var result = await _fileVersionService.CreateInitialVersionAsync(fileItemId);
                return Ok(ApiResponse.Success("Initial version created", result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        // Mô phỏng tài liệu vào SHARED thành công: Revision +1, Version reset 01.
        [HttpPost("{fileItemId:guid}/enter-shared")]
        public async Task<IActionResult> GetNextSharedVersion(Guid fileItemId)
        {
            try
            {
                var result = await _fileVersionService.GetNextSharedVersionAsync(fileItemId);
                return Ok(ApiResponse.Success("Shared version calculated", result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        // Mô phỏng publish: C{PublishedRevision}.
        [HttpPost("{fileItemId:guid}/publish")]
        public async Task<IActionResult> GetNextPublishedVersion(Guid fileItemId)
        {
            try
            {
                var result = await _fileVersionService.GetNextPublishedVersionAsync(fileItemId);
                return Ok(ApiResponse.Success("Published version calculated", result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        // Mô phỏng quay về WIP từ Published: P{Revision}.01, PublishedRevision bảo toàn.
        [HttpPost("{fileItemId:guid}/return-to-wip")]
        public async Task<IActionResult> GetReturnToWipVersion(Guid fileItemId)
        {
            try
            {
                var result = await _fileVersionService.GetReturnToWipVersionAsync(fileItemId);
                return Ok(ApiResponse.Success("Return-to-WIP version calculated", result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        // Toàn bộ lịch sử version (mới nhất trước), kèm snapshot dữ liệu file của từng version.
        [HttpGet("{fileItemId:guid}/history")]
        public async Task<IActionResult> GetVersionHistory(Guid fileItemId)
        {
            var result = await _fileVersionService.GetVersionHistoryAsync(fileItemId);
            return Ok(ApiResponse.Success("Version history retrieved", result));
        }

        // Trạng thái version hiện hành + chuỗi hiển thị đã format.
        [HttpGet("{fileItemId:guid}/current")]
        public async Task<IActionResult> GetCurrentVersion(Guid fileItemId)
        {
            var result = await _fileVersionService.GetCurrentVersionAsync(fileItemId);
            if (result == null)
                return NotFound(ApiResponse.Fail($"FileItem {fileItemId} has no version state."));

            return Ok(ApiResponse.Success("Current version retrieved", result));
        }
    }
}
