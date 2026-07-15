namespace Application.DTOs.ResponseDTOs.ZoneReturn
{
    public class ZoneReturnRequestResponseDTO
    {
        public Guid ReturnRequestId { get; set; }
        public Guid FileId { get; set; }
        public string? FileName { get; set; }
        public string FromZone { get; set; } = string.Empty;
        public string TargetZone { get; set; } = string.Empty;
        public Guid RequestedBy { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
    }
}
