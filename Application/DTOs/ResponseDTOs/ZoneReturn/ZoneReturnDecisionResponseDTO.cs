namespace Application.DTOs.ResponseDTOs.ZoneReturn
{
    public class ZoneReturnDecisionResponseDTO
    {
        public Guid ReturnRequestId { get; set; }
        public Guid FileId { get; set; }
        public string? FromZone { get; set; }
        public string? ToZone { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
    }
}
