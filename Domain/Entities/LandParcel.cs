using Domain.Enum.Site;

using Domain.Common;

namespace Domain.Entities
{
    // Lô đất / hộ giải tỏa — GeoJSON xuất từ autocad địa chính, theo gói thầu
    public class LandParcel : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ContractPackageId { get; set; }
        public string? ParcelCode { get; set; }
        public string? HouseholdName { get; set; }
        public ClearanceStatus ClearanceStatus { get; set; }
        public string GeoJson { get; set; } = null!;
        public Guid? SourceFileVersionId { get; set; }   // file GeoJSON trong kho CDE
        public string? InfoJson { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ContractPackage? ContractPackage { get; set; }
    }
}
