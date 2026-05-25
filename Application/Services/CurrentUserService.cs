using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Http;

namespace Application.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

        public Guid? AccountId
        {
            get
            {
                var v = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return Guid.TryParse(v, out var id) ? id : null;
            }
        }

        public string? UserName => User?.FindFirst("UserName")?.Value;

        public string? Email
            => User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? User?.FindFirst(ClaimTypes.Email)?.Value;

        public string? SystemRole => User?.FindFirst(ClaimTypes.Role)?.Value;

        // Multi-valued claim "DepartmentRole" với value "<departmentId>:<role>".
        public IReadOnlyList<DepartmentMembership> DepartmentMemberships
        {
            get
            {
                if (User == null) return Array.Empty<DepartmentMembership>();
                return User.FindAll("DepartmentRole")
                    .Select(c => c.Value.Split(':', 2))
                    .Where(parts => parts.Length == 2 && Guid.TryParse(parts[0], out _))
                    .Select(parts => new DepartmentMembership(Guid.Parse(parts[0]), parts[1]))
                    .ToList();
            }
        }
    }
}
