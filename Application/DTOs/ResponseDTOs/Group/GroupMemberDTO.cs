using Domain.Enum.Group;

namespace Application.DTOs.ResponseDTOs.Group
{
    // Sub-DTO: 1 thành viên trong Group, kèm Role.
    public class GroupMemberDTO
    {
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = null!;
        public string? Email { get; set; }
        public GroupMemberRole Role { get; set; }
        public DateTime? JoinedAt { get; set; }
    }
}
