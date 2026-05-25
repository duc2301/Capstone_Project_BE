namespace Application.DTOs.ResponseDTOs.OrganizationType
{
    public class OrganizationTypeResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
