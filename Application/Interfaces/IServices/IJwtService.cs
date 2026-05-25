using Domain.Entities;

namespace Application.Interfaces.IServices
{
    // Sinh JWT access token + refresh token (chuỗi ngẫu nhiên, lưu DB).
    public interface IJwtService
    {
        // groupMemberships: các GroupMember rows của account -> đẩy thành multi-valued claim "Group"
        // (value = GroupId) để FE biết user thuộc nhóm nào, không cần query DB.
        string GenerateAccessToken(Account account, IEnumerable<GroupMember>? groupMemberships = null);
        string GenerateRefreshToken();
        int AccessTokenMinutes { get; }
        int RefreshTokenDays { get; }
    }
}
