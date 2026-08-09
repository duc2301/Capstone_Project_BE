using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Account;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateAccountDTO dto)
        {

            var result = await _accountService.CreateAsync(dto, User.GetAccountId());
            return Ok(ApiResponse.Success("Registration successful", result));
        }

        [HttpGet("import-template")]
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadImportTemplate()
        {
            return File(_accountService.GenerateImportTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "account-import-template.xlsx");
        }

        [HttpPost("import")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("Chưa chọn file.", 400);
            if (file.Length > 2 * 1024 * 1024)
                throw new ApiExceptionResponse("File vượt quá 2MB.", 400);
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new ApiExceptionResponse("Chỉ chấp nhận file .xlsx.", 400);

            await using var stream = file.OpenReadStream();
            var result = await _accountService.ImportFromExcelAsync(stream, User.GetAccountId());
            return Ok(ApiResponse.Success("Import completed", result));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var result = await _accountService.GetAllAsync();
            return Ok(ApiResponse.Success("Accounts retrieved successfully", result));

        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountDTO dto)
        {
            var result = await _accountService.UpdateAsync(id, dto, User.GetAccountId());
            return Ok(ApiResponse.Success("Account updated successfully", result));
        }

        [HttpPost("{id:guid}/avatar")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6_291_456)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6_291_456)]
        public async Task<IActionResult> UploadAvatar(Guid id, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("No file provided.", 400);

            await using var stream = file.OpenReadStream();
            var result = await _accountService.SetAvatarAsync(
                id, stream, file.FileName, file.Length, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("Avatar updated successfully", result));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _accountService.DeleteAsync(id, User.GetAccountId());
            return Ok(ApiResponse.Success("Account deleted successfully"));

        }
    }
}
