using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Loi;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/projects")]
    [Authorize]
    public class LoiAliasesController : ControllerBase
    {
        private readonly ILoiAliasService _aliases;

        public LoiAliasesController(ILoiAliasService aliases)
        {
            _aliases = aliases;
        }

        [HttpGet("{projectId:guid}/loi-aliases")]
        public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI aliases retrieved",
                await _aliases.GetByProjectAsync(projectId, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPost("{projectId:guid}/loi-aliases")]
        public async Task<IActionResult> Create(
            Guid projectId, [FromBody] CreateLoiAliasDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI alias created",
                await _aliases.CreateAsync(projectId, dto, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpDelete("{projectId:guid}/loi-aliases/{aliasId:guid}")]
        public async Task<IActionResult> Delete(Guid projectId, Guid aliasId, CancellationToken ct)
        {
            await _aliases.DeleteAsync(projectId, aliasId, User.GetAccountId(), User.IsAdmin(), ct);
            return Ok(ApiResponse.Success("LOI alias deleted"));
        }
    }
}
