using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Loi;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/loi-aliases")]
    [Authorize(Roles = "Admin")]
    public class LoiSystemAliasesController : ControllerBase
    {
        private readonly ILoiRuleAdminService _rules;

        public LoiSystemAliasesController(ILoiRuleAdminService rules)
        {
            _rules = rules;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken ct)
            => Ok(ApiResponse.Success("System LOI aliases retrieved",
                await _rules.GetSystemAliasesAsync(search, ct)));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSystemLoiAliasDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("System LOI alias created",
                await _rules.CreateSystemAliasAsync(dto, User.GetAccountId(), ct)));

        [HttpDelete("{aliasId:guid}")]
        public async Task<IActionResult> Delete(Guid aliasId, CancellationToken ct)
        {
            await _rules.DeleteSystemAliasAsync(aliasId, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("System LOI alias deleted"));
        }
    }
}
