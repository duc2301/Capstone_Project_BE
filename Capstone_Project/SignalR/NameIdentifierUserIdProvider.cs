using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    // SignalR mặc định lấy UserIdentifier = Name claim. Ta đổi sang NameIdentifier (AccountId) để khớp JWT.
    public class NameIdentifierUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
            => connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
