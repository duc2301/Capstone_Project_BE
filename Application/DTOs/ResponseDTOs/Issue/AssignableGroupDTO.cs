namespace Application.DTOs.ResponseDTOs.Issue
{
    public class AssignableGroupDTO
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = null!;
        public string? OrganizationName { get; set; }
        public int MemberCount { get; set; }
    }
}
