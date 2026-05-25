using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Notification;
using Application.DTOs.ResponseDTOs.Notification;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/notifications")]
    public class NotificationsController
        : BaseCrudController<Notification, CreateNotificationDTO, UpdateNotificationDTO, NotificationResponseDTO>
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(
            IGenericService<Notification, CreateNotificationDTO, UpdateNotificationDTO, NotificationResponseDTO> service,
            INotificationService notificationService)
            : base(service)
        {
            _notificationService = notificationService;
        }

        // Dispatcher — emit notification tới một hoặc nhiều account (vd từ mention, approval, ...).
        // Các service nghiệp vụ có thể gọi INotificationService trực tiếp thay vì qua endpoint này;
        // endpoint này phục vụ test/admin push thủ công.
        [HttpPost("dispatch")]
        public async Task<IActionResult> Dispatch([FromBody] DispatchNotificationDTO dto)
        {
            await _notificationService.NotifyManyAsync(
                dto.AccountIds, dto.Message, dto.SenderName, dto.LinkType, dto.LinkId);
            return Ok(ApiResponse.Success("Notifications dispatched"));
        }
    }
}
