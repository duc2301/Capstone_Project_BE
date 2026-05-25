using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Notification
{
    public class DispatchNotificationDTO
    {
        [Required]
        public List<Guid> AccountIds { get; set; } = new();

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = null!;

        [StringLength(150)]
        public string? SenderName { get; set; }

        // Vd "Submittal", "Discussion", "Issue", "FileVersion"
        [StringLength(50)]
        public string? LinkType { get; set; }

        [StringLength(100)]
        public string? LinkId { get; set; }
    }
}
