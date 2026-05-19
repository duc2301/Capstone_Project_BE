namespace Application.DTOs.ResponseDTOs.Notification
{
    public class NotificationResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = null!;
        public DateTime SendAt { get; set; }
        public string SenderName { get; set; } = null!;
        public bool IsRead { get; set; }
        public Guid AccountId { get; set; }
    }
}
