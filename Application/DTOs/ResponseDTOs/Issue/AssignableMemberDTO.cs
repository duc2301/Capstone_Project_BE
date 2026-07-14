namespace Application.DTOs.ResponseDTOs.Issue
{
    public class AssignableMemberDTO
    {
        public Guid AccountId { get; set; }
        public string Name { get; set; } = null!;
        public string? Email { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = null!;
    }
}
