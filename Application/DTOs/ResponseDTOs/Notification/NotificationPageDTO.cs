namespace Application.DTOs.ResponseDTOs.Notification
{
    public class NotificationPageDTO
    {
        public List<NotificationResponseDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public int UnreadCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
