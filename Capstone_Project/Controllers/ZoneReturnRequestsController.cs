using Application.DTOs.RequestDTOs.ZoneReturn;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/zone-return-requests")]
    public class ZoneReturnRequestsController : ControllerBase
    {
        private readonly IZoneReturnRequestService _zoneReturnRequestService;

        public ZoneReturnRequestsController(IZoneReturnRequestService zoneReturnRequestService)
        {
            _zoneReturnRequestService = zoneReturnRequestService;
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
            => Ok(await _zoneReturnRequestService.GetPendingAsync(User.GetAccountId()));

        [HttpPost("{requestId:guid}/approve")]
        public async Task<IActionResult> Approve(Guid requestId)
            => Ok(await _zoneReturnRequestService.ApproveAsync(requestId, User.GetAccountId()));

        [HttpPost("{requestId:guid}/reject")]
        public async Task<IActionResult> Reject(Guid requestId, [FromBody] RejectZoneReturnRequestDTO dto)
            => Ok(await _zoneReturnRequestService.RejectAsync(requestId, dto, User.GetAccountId()));
    }
}
