using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Invitation;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/project-invitations")]
    [ApiController]
    [Authorize]   // AccountId/InvitedBy lấy từ JWT trong header, không nhận trong body
    public class InvitationsController : ControllerBase
    {
        private readonly IInvitationService _invitationService;

        public InvitationsController(IInvitationService invitationService)
        {
            _invitationService = invitationService;
        }

        [HttpPost]
        public async Task<IActionResult> Invite([FromBody] InviteRequestDTO dto)
        {
            var result = await _invitationService.InviteAsync(dto);
            return CreatedAtAction(nameof(Invite), new { id = result.Id },
                ApiResponse.Success("Invitation created", result));
        }

        // Token nằm trên URL (RESTful), account lấy từ JWT -> không cần body.
        [HttpPost("{token}/accept")]
        public async Task<IActionResult> Accept(string token)
        {
            var result = await _invitationService.AcceptAsync(token);
            return Ok(ApiResponse.Success("Invitation accepted", result));
        }

        [HttpPost("{token}/reject")]
        public async Task<IActionResult> Reject(string token)
        {
            var result = await _invitationService.RejectAsync(token);
            return Ok(ApiResponse.Success("Invitation rejected", result));
        }
    }
}
