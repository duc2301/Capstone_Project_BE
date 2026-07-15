using Domain.Enum.ContractPackage;

namespace Application.DTOs.ResponseDTOs.ContractPackage
{
    public class ContractPackageResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal? ContractValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public PackageStatus Status { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? CreatedAt { get; set; }

        public string? WorkTypes { get; set; }
        public string? ScopeDescription { get; set; }
        public decimal? TaxRate { get; set; }
        public string? Currency { get; set; }
        public string? Notes { get; set; }
        public Guid? DocumentFolderId { get; set; }
        
        public ICollection<PackageAssignmentResponseDTO> Assignments { get; set; } = new List<PackageAssignmentResponseDTO>();
    }
}
