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
        public NotificationsController(
            IGenericService<Notification, CreateNotificationDTO, UpdateNotificationDTO, NotificationResponseDTO> service)
            : base(service) { }
    }
}
