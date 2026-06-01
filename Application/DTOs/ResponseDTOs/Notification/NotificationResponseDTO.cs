namespace Application.DTOs.ResponseDTOs.Notification
{
    public class NotificationResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string Message { get; set; } = null!;
        public string SenderName { get; set; } = null!;
        public DateTime SendAt { get; set; }
        public bool IsRead { get; set; }

        // Để FE click vào notification điều hướng đúng chỗ
        public string? LinkType { get; set; }
        public string? LinkId { get; set; }
    }
}
