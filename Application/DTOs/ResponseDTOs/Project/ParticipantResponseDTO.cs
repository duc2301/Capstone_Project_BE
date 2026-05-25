using Domain.Enum.Project;

namespace Application.DTOs.ResponseDTOs.Project
{
    public class ParticipantResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? GroupId { get; set; }
        public ProjectParticipantRole Role { get; set; }
        public DateTime? JoinedAt { get; set; }
    }
}
