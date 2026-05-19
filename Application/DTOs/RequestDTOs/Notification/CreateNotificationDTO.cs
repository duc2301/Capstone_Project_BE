using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Notification
{
    public class CreateNotificationDTO
    {
        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = null!;

        [StringLength(150)]
        public string? SenderName { get; set; }

        [Required]
        public Guid AccountId { get; set; }
    }
}
