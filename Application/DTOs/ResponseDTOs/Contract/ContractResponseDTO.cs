using Domain.Enum.Contract;

namespace Application.DTOs.ResponseDTOs.Contract
{
    public class ContractResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ContractPackageId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public Guid? ContractorOrganizationId { get; set; }
        public Guid? SourceFileVersionId { get; set; }
        public DateTime? SignedDate { get; set; }
        public ContractStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
