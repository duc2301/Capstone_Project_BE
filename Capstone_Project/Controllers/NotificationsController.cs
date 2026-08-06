using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Notification;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Notification của user hiện tại (JWT) — không nhận accountId từ ngoài
        [HttpGet("me")]
        public async Task<IActionResult> GetMine([FromQuery] NotificationFilterDTO filter)
        {
            var result = await _notificationService.GetMyAsync(User.GetAccountId(), filter);
            return Ok(ApiResponse.Success("Notifications retrieved", result));
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var count = await _notificationService.MarkAllReadAsync(User.GetAccountId());
            return Ok(ApiResponse.Success($"{count} notification(s) marked as read"));
        }

        // Đánh dấu đã đọc 1 notification (chỉ chính chủ)
        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            await _notificationService.MarkReadAsync(id, User.GetAccountId());
            return Ok(ApiResponse.Success("Notification marked as read"));
        }

        // Note: endpoint /dispatch đã bỏ. Notification được phát tự động từ các service nghiệp vụ
        // (InvitationService, SubmittalService, DiscussionService...) thông qua INotificationService + SignalR.
    }
}
