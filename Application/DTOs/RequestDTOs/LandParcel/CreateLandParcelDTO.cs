using System.ComponentModel.DataAnnotations;
using Domain.Enum.Site;

namespace Application.DTOs.RequestDTOs.LandParcel
{
    public class CreateLandParcelDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        public Guid? ContractPackageId { get; set; }

        [StringLength(100)]
        public string? ParcelCode { get; set; }

        [StringLength(250)]
        public string? HouseholdName { get; set; }

        [Required]
        public ClearanceStatus ClearanceStatus { get; set; }

        [Required]
        public string GeoJson { get; set; } = null!;

        public Guid? SourceFileVersionId { get; set; }
        public string? InfoJson { get; set; }
    }
}
