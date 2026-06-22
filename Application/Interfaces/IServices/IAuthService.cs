using Application.DTOs.RequestDTOs.Auth;
using Application.DTOs.ResponseDTOs.Auth;

namespace Application.Interfaces.IServices
{
    // Auth giống ChemXLab (Register/Login bằng BCrypt + JWT) + refresh token xoay vòng.
    public interface IAuthService
    {
        Task<AuthResponseDTO> Register(RegisterDTO request);
        Task<AuthResponseDTO> Login(LoginDTO request);
        Task<AuthResponseDTO> GoogleLogin(GoogleLoginDTO request);
        Task<AuthResponseDTO> Refresh(RefreshTokenRequestDTO request);
        Task Logout(RefreshTokenRequestDTO request);
        Task ForgotPassword(ForgotPasswordDTO request);
        Task ResetPassword(ResetPasswordDTO request);
    }
}
