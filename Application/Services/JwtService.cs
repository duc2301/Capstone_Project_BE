using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Application.Services
{
    // Giống JwtService của ChemXLab, thích ứng entity Account + bổ sung refresh token
    // và multi-valued "DepartmentRole" claim cho từng phòng ban account thuộc.
    public class JwtService : IJwtService
    {
        private readonly string _key;
        private readonly string _issuer;
        private readonly string _audience;

        public int AccessTokenMinutes { get; }
        public int RefreshTokenDays { get; }

        public JwtService(IConfiguration config)
        {
            _key = config["Jwt:Key"]!;
            _issuer = config["Jwt:Issuer"]!;
            _audience = config["Jwt:Audience"]!;
            AccessTokenMinutes = int.Parse(config["Jwt:ExpireMinutes"] ?? "120");
            RefreshTokenDays = int.Parse(config["Jwt:RefreshTokenDays"] ?? "7");
        }

        public string GenerateAccessToken(Account account, IEnumerable<Employee>? employments = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, account.Email),
                new("UserName", account.UserName),
                new(ClaimTypes.Role, account.Role?.ToString() ?? "User"),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Mỗi Employee row có DepartmentId -> claim "DepartmentRole" = "<deptId>:<role>"
            if (employments != null)
            {
                foreach (var e in employments)
                {
                    if (e.DepartmentId.HasValue)
                        claims.Add(new Claim("DepartmentRole", $"{e.DepartmentId}:{e.Role}"));
                }
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _issuer,
                _audience,
                claims,
                expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Chuỗi ngẫu nhiên 64 byte, đối chiếu trong DB (không tự giải mã được).
        public string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }
}
