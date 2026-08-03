using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Loi;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/file-items")]
    [Authorize]
    public class LoiChecksController : ControllerBase
    {
        private readonly ILoiCheckService _loi;

        public LoiChecksController(ILoiCheckService loi)
        {
            _loi = loi;
        }

        [HttpGet("{fileId:guid}/loi-check")]
        public async Task<IActionResult> Get(Guid fileId, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI check result",
                await _loi.GetByFileItemAsync(fileId, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPost("{fileId:guid}/loi-check/recompute")]
        public async Task<IActionResult> Recompute(
            Guid fileId, [FromBody] RecomputeLoiCheckDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("LOI re-check queued",
                await _loi.RecomputeAsync(fileId, dto.Stage, User.GetAccountId(), User.IsAdmin(), ct)));
    }
}
