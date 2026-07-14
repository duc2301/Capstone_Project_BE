using Domain.Enum.ContractPackage;

namespace Application.DTOs.ResponseDTOs.ContractPackage
{
    public class PackageAssignmentResponseDTO
    {
        public Guid Id { get; set; }
        public Guid ContractPackageId { get; set; }
        public Guid OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
        public string? OrganizationCode { get; set; }
        public PackageRole Role { get; set; }
        public string? ContractNumber { get; set; }
        public Guid? RepresentativeAccountId { get; set; }
        public string? RepresentativeName { get; set; }
        public string? RepresentativeEmail { get; set; }
        public string? RepresentativePhone { get; set; }
        public string? Position { get; set; }
        public string? VatCode { get; set; }
        public DateTime? ContractSignDate { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
