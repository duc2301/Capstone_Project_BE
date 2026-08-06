namespace Application.DTOs.RequestDTOs.Notification
{
    public class NotificationFilterDTO
    {
        public bool? UnreadOnly { get; set; }
        public string? LinkType { get; set; }
        public string? Search { get; set; }
        public DateTime? From { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
