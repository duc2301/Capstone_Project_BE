using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Profile;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/profile")]
    [ApiController]
    [Authorize]   // mọi endpoint đều thao tác trên chính user hiện tại (JWT)
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        // GET /api/profile -> thông tin user hiện tại + danh sách group memberships
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await _profileService.GetMyProfileAsync();
            return Ok(ApiResponse.Success("Profile retrieved", result));
        }

        // PUT /api/profile -> partial update UserName/Email (Role/Status admin-only ở AccountController)
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateProfileDTO dto)
        {
            var result = await _profileService.UpdateMyProfileAsync(dto);
            return Ok(ApiResponse.Success("Profile updated", result));
        }

        // POST /api/profile/change-password -> verify current + đổi mật khẩu
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
        {
            await _profileService.ChangePasswordAsync(dto);
            return Ok(ApiResponse.Success("Password changed"));
        }
    }
}
