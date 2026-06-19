using System.Security.Claims;
using Application.ExceptionMiddleware;
using Domain.Enum.Account;

namespace Capstone_Project.Extensions
{
    // Đọc thông tin user hiện tại trực tiếp từ claim JWT (đã được middleware xác thực gắn vào User).
    // Thay cho CurrentUserService: controller đọc ở đây rồi truyền xuống service qua tham số.
    public static class ClaimsPrincipalExtensions
    {
        // AccountId bắt buộc — ném 401 nếu request không kèm token hợp lệ.
        public static Guid GetAccountId(this ClaimsPrincipal user)
            => user.GetAccountIdOrNull()
               ?? throw new ApiExceptionResponse("Authentication required.", 401);

        public static Guid? GetAccountIdOrNull(this ClaimsPrincipal user)
        {
            var v = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(v, out var id) ? id : null;
        }

        public static string? GetSystemRole(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Role);

        public static bool IsAdmin(this ClaimsPrincipal user)
            => user.GetSystemRole() == AccountRole.Admin.ToString();

        public static string? GetUserName(this ClaimsPrincipal user)
            => user.FindFirst("UserName")?.Value;
    }
}
