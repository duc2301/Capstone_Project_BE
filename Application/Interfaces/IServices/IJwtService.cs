using Domain.Entities;

namespace Application.Interfaces.IServices
{
    // Sinh JWT access token + refresh token (chuỗi ngẫu nhiên, lưu DB).
    public interface IJwtService
    {
        // employments: các Employee rows của account -> đẩy thành multi-valued claim "DepartmentRole"
        // để FE/service đọc được role trong từng phòng ban từ token, không cần query DB.
        string GenerateAccessToken(Account account, IEnumerable<Employee>? employments = null);
        string GenerateRefreshToken();
        int AccessTokenMinutes { get; }
        int RefreshTokenDays { get; }
    }
}
