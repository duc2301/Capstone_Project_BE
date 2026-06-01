namespace Application.DTOs.ResponseDTOs.Group
{
    public class GroupResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid? OrganizationId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public IList<GroupMemberDTO> Members { get; set; } = new List<GroupMemberDTO>();
    }
}
