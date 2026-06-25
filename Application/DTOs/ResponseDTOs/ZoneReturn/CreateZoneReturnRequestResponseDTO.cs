namespace Application.DTOs.ResponseDTOs.ZoneReturn
{
    public class CreateZoneReturnRequestResponseDTO
    {
        public Guid ReturnRequestId { get; set; }
        public Guid FileId { get; set; }
        public string FromZone { get; set; } = string.Empty;
        public string TargetZone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
