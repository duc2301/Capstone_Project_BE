using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Capstone_Project.SignalR
{
    // Hub realtime gửi notification về browser của 1 user.
    // Auth bằng JWT: client connect với access_token query string -> JWT middleware decode -> Context.UserIdentifier = AccountId.
    // Tên method client cần subscribe: "ReceiveNotification".
    [Authorize]
    public class NotificationHub : Hub
    {
        // Không cần override gì thêm. Server-side push qua IHubContext<NotificationHub>.
    }
}
