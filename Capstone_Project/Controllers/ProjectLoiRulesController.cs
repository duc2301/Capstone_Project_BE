using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Loi;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Domain.Enum.Loi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId:guid}/loi-rules")]
    [Authorize]
    public class ProjectLoiRulesController : ControllerBase
    {
        private const long MaxImportFileBytes = 8 * 1024 * 1024;
        private const string ExcelContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IProjectLoiRuleService _rules;

        public ProjectLoiRulesController(IProjectLoiRuleService rules)
        {
            _rules = rules;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetRuleSetSummary(Guid projectId, CancellationToken ct)
            => Ok(ApiResponse.Success("Project LOI rule set summary retrieved",
                await _rules.GetRuleSetSummaryAsync(projectId, ct)));

        [HttpGet]
        public async Task<IActionResult> GetRuleSet(Guid projectId, CancellationToken ct)
            => Ok(ApiResponse.Success("Project LOI rule set retrieved",
                await _rules.GetRuleSetAsync(projectId, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPut]
        public async Task<IActionResult> UpdateRuleSet(
            Guid projectId, [FromBody] UpdateLoiRuleSetDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("Project LOI rule set updated",
                await _rules.UpdateRuleSetAsync(projectId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpDelete]
        public async Task<IActionResult> DeleteRuleSet(Guid projectId, CancellationToken ct)
        {
            await _rules.DeleteRuleSetAsync(projectId, User.GetAccountId(), User.IsAdmin(), ct);
            return Ok(ApiResponse.Success("Project LOI rule set deleted"));
        }

        [HttpGet("import-template")]
        public async Task<IActionResult> DownloadTemplate(Guid projectId, CancellationToken ct)
            => File(
                await _rules.GenerateTemplateAsync(projectId, User.GetAccountId(), User.IsAdmin(), ct),
                ExcelContentType,
                "bo-luat-phi-hinh-hoc-mau.xlsx");

        [HttpPost("import-preview")]
        public async Task<IActionResult> ImportPreview(Guid projectId, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Chưa chọn file."));
            if (file.Length > MaxImportFileBytes)
                return BadRequest(ApiResponse.Fail("File vượt quá 8MB."));
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse.Fail("Chỉ chấp nhận file .xlsx."));

            await using var stream = file.OpenReadStream();
            return Ok(ApiResponse.Success("Parsed successfully",
                await _rules.ParseImportAsync(projectId, stream, User.GetAccountId(), User.IsAdmin(), ct)));
        }

        [HttpPost("import-commit")]
        public async Task<IActionResult> ImportCommit(
            Guid projectId, [FromBody] LoiImportCommitDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI rule set imported",
                await _rules.CommitImportAsync(projectId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpGet("components")]
        public async Task<IActionResult> GetComponents(
            Guid projectId, [FromQuery] LoiDiscipline? discipline, [FromQuery] string? search, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI components retrieved",
                await _rules.GetComponentsAsync(
                    projectId, discipline, search, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPost("components")]
        public async Task<IActionResult> CreateComponent(
            Guid projectId, [FromBody] CreateLoiComponentDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI component created",
                await _rules.CreateComponentAsync(projectId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPut("components/{componentId:guid}")]
        public async Task<IActionResult> UpdateComponent(
            Guid projectId, Guid componentId, [FromBody] UpdateLoiComponentDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI component updated",
                await _rules.UpdateComponentAsync(
                    projectId, componentId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpDelete("components/{componentId:guid}")]
        public async Task<IActionResult> DeleteComponent(Guid projectId, Guid componentId, CancellationToken ct)
        {
            await _rules.DeleteComponentAsync(projectId, componentId, User.GetAccountId(), User.IsAdmin(), ct);
            return Ok(ApiResponse.Success("LOI component deleted"));
        }

        [HttpGet("components/{componentId:guid}/matrix")]
        public async Task<IActionResult> GetMatrix(Guid projectId, Guid componentId, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI matrix retrieved",
                await _rules.GetMatrixAsync(projectId, componentId, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPut("components/{componentId:guid}/matrix")]
        public async Task<IActionResult> SaveMatrix(
            Guid projectId, Guid componentId, [FromBody] SaveLoiMatrixDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI matrix saved",
                await _rules.SaveMatrixAsync(
                    projectId, componentId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPut("components/{componentId:guid}/variant")]
        public async Task<IActionResult> RenameVariant(
            Guid projectId, Guid componentId, [FromBody] RenameLoiVariantDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI variant renamed",
                await _rules.RenameVariantAsync(
                    projectId, componentId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpDelete("components/{componentId:guid}/variant")]
        public async Task<IActionResult> DeleteVariant(
            Guid projectId, Guid componentId, [FromQuery] string? variant, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI variant deleted",
                await _rules.DeleteVariantAsync(
                    projectId, componentId, variant, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpGet("parameters")]
        public async Task<IActionResult> GetParameters(Guid projectId, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI parameters retrieved",
                await _rules.GetParametersAsync(projectId, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPost("parameters")]
        public async Task<IActionResult> CreateParameter(
            Guid projectId, [FromBody] CreateLoiParameterDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI parameter created",
                await _rules.CreateParameterAsync(projectId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPut("parameters/{parameterId:guid}")]
        public async Task<IActionResult> UpdateParameter(
            Guid projectId, Guid parameterId, [FromBody] UpdateLoiParameterDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI parameter updated",
                await _rules.UpdateParameterAsync(
                    projectId, parameterId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpDelete("parameters/{parameterId:guid}")]
        public async Task<IActionResult> DeleteParameter(Guid projectId, Guid parameterId, CancellationToken ct)
        {
            await _rules.DeleteParameterAsync(projectId, parameterId, User.GetAccountId(), User.IsAdmin(), ct);
            return Ok(ApiResponse.Success("LOI parameter deleted"));
        }
    }
}
