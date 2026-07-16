using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.NamingConvention;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    // Cấu hình naming convention (admin) + payload cho dialog upload.
    // Lưu ý: chưa kiểm tra quyền (iteration sau) — mọi request đã đăng nhập đều được phép.
    [Route("api/naming-conventions")]
    [Authorize]
    public class NamingConventionsController : ControllerBase
    {
        private readonly INamingConventionService _service;

        public NamingConventionsController(INamingConventionService service)
        {
            _service = service;
        }

        // ============ Convention ============

        // Tạo convention mức dự án, kèm sẵn fields + allowed values (+ locked value theo Code).
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNamingConventionDTO dto)
        {
            var result = await _service.CreateAsync(dto, User.GetAccountId());
            return Ok(ApiResponse.Success("Naming convention created", result));
        }

        // Danh sách convention của 1 dự án (trang cấu hình).
        [HttpGet("projects/{projectId:guid}")]
        public async Task<IActionResult> GetByProject(Guid projectId)
        {
            return Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByProjectAsync(projectId)));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            return Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));
        }

        // Đổi tên / delimiter / bật-tắt convention.
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNamingConventionDTO dto)
        {
            return Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }

        // ============ Fields ============

        [HttpPost("{id:guid}/fields")]
        public async Task<IActionResult> AddField(Guid id, [FromBody] CreateNamingFieldDTO dto)
        {
            var result = await _service.AddFieldAsync(id, dto, User.GetAccountId());
            return Ok(ApiResponse.Success("Field added", result));
        }

        [HttpPut("fields/{fieldId:guid}")]
        public async Task<IActionResult> UpdateField(Guid fieldId, [FromBody] UpdateNamingFieldDTO dto)
        {
            return Ok(ApiResponse.Success("Field updated", await _service.UpdateFieldAsync(fieldId, dto)));
        }

        [HttpDelete("fields/{fieldId:guid}")]
        public async Task<IActionResult> DeleteField(Guid fieldId)
        {
            await _service.DeleteFieldAsync(fieldId);
            return Ok(ApiResponse.Success("Field deleted"));
        }

        // ============ Allowed values ============

        [HttpPost("fields/{fieldId:guid}/values")]
        public async Task<IActionResult> AddFieldValues(Guid fieldId, [FromBody] List<CreateNamingFieldValueDTO> dtos)
        {
            var result = await _service.AddFieldValuesAsync(fieldId, dtos, User.GetAccountId());
            return Ok(ApiResponse.Success("Values added", result));
        }

        [HttpPut("values/{valueId:guid}")]
        public async Task<IActionResult> UpdateFieldValue(Guid valueId, [FromBody] UpdateNamingFieldValueDTO dto)
        {
            return Ok(ApiResponse.Success("Value updated", await _service.UpdateFieldValueAsync(valueId, dto)));
        }

        [HttpDelete("values/{valueId:guid}")]
        public async Task<IActionResult> DeleteFieldValue(Guid valueId)
        {
            await _service.DeleteFieldValueAsync(valueId);
            return Ok(ApiResponse.Success("Value deleted"));
        }

        // ============ Locked value ============

        // Khóa field vào 1 value cố định: FE ẩn dropdown, BE luôn tự chèn value này.
        [HttpPut("fields/{fieldId:guid}/locked-value")]
        public async Task<IActionResult> SetLockedValue(Guid fieldId, [FromBody] SetLockedValueDTO dto)
        {
            var result = await _service.SetLockedValueAsync(fieldId, dto, User.GetAccountId());
            return Ok(ApiResponse.Success("Field locked", result));
        }

        [HttpDelete("fields/{fieldId:guid}/locked-value")]
        public async Task<IActionResult> RemoveLockedValue(Guid fieldId)
        {
            return Ok(ApiResponse.Success("Field unlocked", await _service.RemoveLockedValueAsync(fieldId)));
        }

        // ============ Folder assignment ============

        // Gán convention cho nhiều folder (tùy chọn áp cho cả cây thư mục con).
        [HttpPost("{id:guid}/folders")]
        public async Task<IActionResult> AssignFolders(Guid id, [FromBody] AssignFoldersDTO dto)
        {
            return Ok(ApiResponse.Success("Folders assigned",
                await _service.AssignFoldersAsync(id, dto, User.GetAccountId(), User.GetSystemRole())));
        }

        // Gỡ convention khỏi 1 folder (folder upload bình thường trở lại).
        [HttpDelete("folders/{folderId:guid}/assignment")]
        public async Task<IActionResult> UnassignFolder(Guid folderId)
        {
            await _service.UnassignFolderAsync(folderId, User.GetAccountId(), User.GetSystemRole());
            return Ok(ApiResponse.Success("Folder unassigned"));
        }

        // ============ Upload dialog ============

        // Dialog upload gọi trước tiên: convention đang áp cho folder + fields/values để render dropdown.
        [HttpGet("folders/{folderId:guid}")]
        public async Task<IActionResult> GetByFolder(Guid folderId)
        {
            return Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByFolderAsync(folderId)));
        }

        [HttpGet("import-template")]
        public IActionResult DownloadImportTemplate()
        {
            return File(_service.GenerateImportTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "naming-convention-template_ISO-19650.xlsx");
        }

        [HttpPost("import-preview")]
        public IActionResult ImportPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Chưa chọn file."));
            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("File vượt quá 2MB."));
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse.Fail("Chỉ chấp nhận file .xlsx."));

            using var stream = file.OpenReadStream();
            return Ok(ApiResponse.Success("Parsed successfully", _service.ParseImportFile(stream)));
        }

        [HttpPost("{id:guid}/clone-for-folder")]
        public async Task<IActionResult> CloneForFolder(Guid id, [FromBody] CloneForFolderDTO dto)
        {
            var result = await _service.CloneForFolderAsync(id, dto.FolderId, User.GetAccountId(), User.GetSystemRole());
            return Ok(ApiResponse.Success("Convention cloned for folder", result));
        }
    }
}
