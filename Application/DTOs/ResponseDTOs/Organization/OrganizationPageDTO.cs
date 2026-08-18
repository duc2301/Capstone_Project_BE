namespace Application.DTOs.ResponseDTOs.Organization
{
    public class OrganizationPageDTO
    {
        public List<OrganizationResponseDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
