using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    // Panel lịch sử version của FE: xem lịch sử, xem version hiện hành, khôi phục version cũ.
    // Các endpoint mô phỏng dùng cho DEV/TEST (next-upload, initial, enter-shared, publish,
    // return-to-wip) đã được gỡ bỏ — những bước đó chạy trong luồng thật của
    // FileUploadService / ApprovalService / FileItemService, không gọi qua controller này.
    [Route("api/file-versions")]
    [Authorize]
    public class FileVersionController : ControllerBase
    {
        private readonly IFileVersionService _fileVersionService;

        public FileVersionController(IFileVersionService fileVersionService)
        {
            _fileVersionService = fileVersionService;
        }

        // Khôi phục 1 version cũ làm version hiện hành: tạo bản mới (P{rev}.{ver+1}) copy dữ liệu
        // file của version được chọn, đồng thời trỏ FileItem.CurrentVersionId sang bản vừa tạo.
        [HttpPost("{fileItemId:guid}/restore/{versionStateId:guid}")]
        public async Task<IActionResult> RestoreVersion(Guid fileItemId, Guid versionStateId)
        {
            try
            {
                var result = await _fileVersionService.RestoreVersionAsync(fileItemId, versionStateId);
                return Ok(ApiResponse.Success("Version restored", result));
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
