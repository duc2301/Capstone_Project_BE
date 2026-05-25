using System.ComponentModel.DataAnnotations;
using Domain.Enum.Site;

namespace Application.DTOs.RequestDTOs.LandParcel
{
    public class UpdateLandParcelDTO
    {
        public Guid? ContractPackageId { get; set; }

        [StringLength(100)]
        public string? ParcelCode { get; set; }

        [StringLength(250)]
        public string? HouseholdName { get; set; }

        public ClearanceStatus? ClearanceStatus { get; set; }
        public string? GeoJson { get; set; }
        public Guid? SourceFileVersionId { get; set; }
        public string? InfoJson { get; set; }
    }
}
