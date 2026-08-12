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
    [Route("api/loi-rule-sets")]
    [Authorize(Roles = "Admin")]
    public class LoiRuleSetsController : ControllerBase
    {
        private const long MaxImportFileBytes = 8 * 1024 * 1024;

        private readonly ILoiRuleAdminService _rules;
        private readonly ILoiRuleImportService _import;

        public LoiRuleSetsController(ILoiRuleAdminService rules, ILoiRuleImportService import)
        {
            _rules = rules;
            _import = import;
        }

        [HttpGet("{ruleSetId:guid}/import-template")]
        public async Task<IActionResult> DownloadTemplate(Guid ruleSetId, CancellationToken ct)
            => File(await _import.GenerateTemplateAsync(ruleSetId, ct),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "loi-rule-set-template.xlsx");

        [HttpPost("import-preview")]
        public async Task<IActionResult> ImportPreview(
            IFormFile file, [FromQuery] Guid? ruleSetId, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Chưa chọn file."));
            if (file.Length > MaxImportFileBytes)
                return BadRequest(ApiResponse.Fail("File vượt quá 8MB."));
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse.Fail("Chỉ chấp nhận file .xlsx."));

            await using var stream = file.OpenReadStream();
            return Ok(ApiResponse.Success("Parsed successfully", await _import.ParseAsync(stream, ruleSetId, ct)));
        }

        [HttpPost("import-commit")]
        public async Task<IActionResult> ImportCommit([FromBody] LoiImportCommitDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI rule set imported",
                await _import.CommitAsync(dto, User.GetAccountId(), ct)));

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(ApiResponse.Success("LOI rule sets retrieved", await _rules.GetRuleSetsAsync(ct)));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLoiRuleSetDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI rule set created",
                await _rules.CreateRuleSetAsync(dto, User.GetAccountId(), ct)));

        [HttpPut("{ruleSetId:guid}")]
        public async Task<IActionResult> Update(
            Guid ruleSetId, [FromBody] UpdateLoiRuleSetDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI rule set updated",
                await _rules.UpdateRuleSetAsync(ruleSetId, dto, User.GetAccountId(), ct)));

        [HttpDelete("{ruleSetId:guid}")]
        public async Task<IActionResult> Delete(Guid ruleSetId, CancellationToken ct)
        {
            await _rules.DeleteRuleSetAsync(ruleSetId, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("LOI rule set deleted"));
        }

        [HttpPut("{ruleSetId:guid}/default")]
        public async Task<IActionResult> SetDefault(Guid ruleSetId, CancellationToken ct)
            => Ok(ApiResponse.Success("Default LOI rule set updated",
                await _rules.SetDefaultRuleSetAsync(ruleSetId, User.GetAccountId(), ct)));

        [HttpGet("{ruleSetId:guid}/components")]
        public async Task<IActionResult> GetComponents(
            Guid ruleSetId, [FromQuery] LoiDiscipline? discipline, [FromQuery] string? search, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI components retrieved",
                await _rules.GetComponentsAsync(ruleSetId, discipline, search, ct)));

        [HttpPost("{ruleSetId:guid}/components")]
        public async Task<IActionResult> CreateComponent(
            Guid ruleSetId, [FromBody] CreateLoiComponentDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI component created",
                await _rules.CreateComponentAsync(ruleSetId, dto, User.GetAccountId(), ct)));

        [HttpPut("{ruleSetId:guid}/components/{componentId:guid}")]
        public async Task<IActionResult> UpdateComponent(
            Guid ruleSetId, Guid componentId, [FromBody] UpdateLoiComponentDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI component updated",
                await _rules.UpdateComponentAsync(ruleSetId, componentId, dto, User.GetAccountId(), ct)));

        [HttpDelete("{ruleSetId:guid}/components/{componentId:guid}")]
        public async Task<IActionResult> DeleteComponent(Guid ruleSetId, Guid componentId, CancellationToken ct)
        {
            await _rules.DeleteComponentAsync(ruleSetId, componentId, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("LOI component deleted"));
        }

        [HttpGet("{ruleSetId:guid}/components/{componentId:guid}/matrix")]
        public async Task<IActionResult> GetMatrix(Guid ruleSetId, Guid componentId, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI matrix retrieved",
                await _rules.GetMatrixAsync(ruleSetId, componentId, ct)));

        [HttpPut("{ruleSetId:guid}/components/{componentId:guid}/matrix")]
        public async Task<IActionResult> SaveMatrix(
            Guid ruleSetId, Guid componentId, [FromBody] SaveLoiMatrixDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI matrix saved",
                await _rules.SaveMatrixAsync(ruleSetId, componentId, dto, User.GetAccountId(), ct)));

        [HttpPut("{ruleSetId:guid}/components/{componentId:guid}/variant")]
        public async Task<IActionResult> RenameVariant(
            Guid ruleSetId, Guid componentId, [FromBody] RenameLoiVariantDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI variant renamed",
                await _rules.RenameVariantAsync(ruleSetId, componentId, dto, User.GetAccountId(), ct)));

        [HttpDelete("{ruleSetId:guid}/components/{componentId:guid}/variant")]
        public async Task<IActionResult> DeleteVariant(
            Guid ruleSetId, Guid componentId, [FromQuery] string? variant, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI variant deleted",
                await _rules.DeleteVariantAsync(ruleSetId, componentId, variant, User.GetAccountId(), ct)));

        [HttpGet("{ruleSetId:guid}/parameters")]
        public async Task<IActionResult> GetParameters(Guid ruleSetId, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI parameters retrieved",
                await _rules.GetParametersAsync(ruleSetId, ct)));

        [HttpPost("{ruleSetId:guid}/parameters")]
        public async Task<IActionResult> CreateParameter(
            Guid ruleSetId, [FromBody] CreateLoiParameterDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI parameter created",
                await _rules.CreateParameterAsync(ruleSetId, dto, User.GetAccountId(), ct)));

        [HttpPut("{ruleSetId:guid}/parameters/{parameterId:guid}")]
        public async Task<IActionResult> UpdateParameter(
            Guid ruleSetId, Guid parameterId, [FromBody] UpdateLoiParameterDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI parameter updated",
                await _rules.UpdateParameterAsync(ruleSetId, parameterId, dto, User.GetAccountId(), ct)));

        [HttpDelete("{ruleSetId:guid}/parameters/{parameterId:guid}")]
        public async Task<IActionResult> DeleteParameter(Guid ruleSetId, Guid parameterId, CancellationToken ct)
        {
            await _rules.DeleteParameterAsync(ruleSetId, parameterId, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("LOI parameter deleted"));
        }
    }
}
