namespace Application.DTOs.ResponseDTOs.Issue
{
    public class AssignableOrganizationDTO
    {
        public Guid OrganizationId { get; set; }
        public string OrganizationName { get; set; } = null!;
        public List<string> GroupNames { get; set; } = new();
    }
}
