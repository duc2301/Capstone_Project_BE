using Domain.Enum.Department;

namespace Application.DTOs.ResponseDTOs.Organization
{
    public class OrganizationResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string TaxCode { get; set; } = null!;
        public string LegalName { get; set; } = null!;
        public string? DisplayName { get; set; }
        public DepartmentType Type { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
