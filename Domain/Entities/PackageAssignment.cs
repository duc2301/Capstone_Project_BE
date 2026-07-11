using Domain.Enum.ContractPackage;

namespace Domain.Entities
{
    // Gán đối tác (nhà thầu/tư vấn giám sát...) vào 1 gói thầu
    public class PackageAssignment
    {
        public Guid Id { get; set; }
        public Guid ContractPackageId { get; set; }
        public Guid OrganizationId { get; set; }
        public PackageRole Role { get; set; }
        public string? ContractNumber { get; set; }
        public Guid? RepresentativeAccountId { get; set; }   // người đại diện (Account)
        public string? Position { get; set; }                // chức danh
        public string? VatCode { get; set; }
        public DateTime? ContractSignDate { get; set; }
        public DateTime? CreatedAt { get; set; }

        public ContractPackage ContractPackage { get; set; } = null!;
        public Organization Organization { get; set; } = null!;
    }
}
