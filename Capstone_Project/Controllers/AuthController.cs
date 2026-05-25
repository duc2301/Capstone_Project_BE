using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Auth;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO request)
        {
            var result = await _authService.Register(request);
            return Ok(ApiResponse.Success("Registration successful", result));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO request)
        {
            var result = await _authService.Login(request);
            return Ok(ApiResponse.Success("Login successful", result));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDTO request)
        {
            var result = await _authService.Refresh(request);
            return Ok(ApiResponse.Success("Token refreshed", result));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDTO request)
        {
            await _authService.Logout(request);
            return Ok(ApiResponse.Success("Logged out"));
        }
    }
}
