using Domain.Entities;

namespace Application.Interfaces.IServices
{
    // Sinh JWT access token + refresh token (chuỗi ngẫu nhiên, lưu DB).
    public interface IJwtService
    {
        string GenerateAccessToken(Account account);
        string GenerateRefreshToken();
        int AccessTokenMinutes { get; }
        int RefreshTokenDays { get; }
    }
}
