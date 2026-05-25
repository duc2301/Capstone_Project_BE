using Domain.Enum.Site;

namespace Application.DTOs.ResponseDTOs.LandParcel
{
    public class LandParcelResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ContractPackageId { get; set; }
        public string? ParcelCode { get; set; }
        public string? HouseholdName { get; set; }
        public ClearanceStatus ClearanceStatus { get; set; }
        public string GeoJson { get; set; } = null!;
        public Guid? SourceFileVersionId { get; set; }
        public string? InfoJson { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
