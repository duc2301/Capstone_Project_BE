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

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDTO request)
        {
            var result = await _authService.GoogleLogin(request);
            return Ok(ApiResponse.Success("Google login successful", result));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO request)
        {
            await _authService.ForgotPassword(request);
            return Ok(ApiResponse.Success("Nếu email tồn tại, link đặt lại mật khẩu đã được gửi."));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO request)
        {
            await _authService.ResetPassword(request);
            return Ok(ApiResponse.Success("Đặt lại mật khẩu thành công."));
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDTO request)
        {
            var result = await _authService.VerifyOtp(request);
            return Ok(ApiResponse.Success("Xác thực email thành công.", result));
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDTO request)
        {
            await _authService.ResendOtp(request);
            return Ok(ApiResponse.Success("Mã OTP mới đã được gửi đến email của bạn."));
        }
    }
}
