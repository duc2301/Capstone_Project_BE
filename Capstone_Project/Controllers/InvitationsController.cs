using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Invitation;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/project-invitations")]
    [ApiController]
    [Authorize]   // AccountId/InvitedBy lấy từ JWT, không nhận trong body
    public class InvitationsController : ControllerBase
    {
        private readonly IInvitationService _invitationService;

        public InvitationsController(IInvitationService invitationService)
        {
            _invitationService = invitationService;
        }

        // PM mời 1 account vào 1 group với role Leader/Member
        [HttpPost]
        public async Task<IActionResult> Invite([FromBody] InviteRequestDTO dto)
        {
            var result = await _invitationService.InviteAsync(dto);
            return CreatedAtAction(nameof(Invite), new { id = result.Id },
                ApiResponse.Success("Invitation created", result));
        }

        // Lời mời Pending của user hiện tại (UI "Lời mời của tôi")
        [HttpGet("me")]
        public async Task<IActionResult> GetMine()
        {
            var result = await _invitationService.GetMyPendingAsync();
            return Ok(ApiResponse.Success("Pending invitations retrieved", result));
        }

        // Accept by InvitationId — JWT chứng minh danh tính, không cần token URL
        [HttpPost("{id:guid}/accept")]
        public async Task<IActionResult> Accept(Guid id)
        {
            var result = await _invitationService.AcceptAsync(id);
            return Ok(ApiResponse.Success("Invitation accepted", result));
        }

        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> Reject(Guid id)
        {
            var result = await _invitationService.RejectAsync(id);
            return Ok(ApiResponse.Success("Invitation rejected", result));
        }
    }
}
