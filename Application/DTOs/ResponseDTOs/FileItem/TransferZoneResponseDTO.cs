namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class TransferZoneResponseDTO
    {
        public Guid FileId { get; set; }
        public string FromZone { get; set; } = string.Empty;
        public string ToZone { get; set; } = string.Empty;
        public Guid FolderId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
