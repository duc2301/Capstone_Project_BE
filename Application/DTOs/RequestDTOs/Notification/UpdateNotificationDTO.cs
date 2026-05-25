using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Notification
{
    public class UpdateNotificationDTO
    {
        [StringLength(1000)]
        public string? Message { get; set; }

        public bool? IsRead { get; set; }
    }
}
