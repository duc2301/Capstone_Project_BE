using Domain.Enum.Project;

namespace Application.DTOs.ResponseDTOs.PermissionChecking
{
    public class CurrentUserParticipantDTO
    {
        public Guid ProjectParticipantId { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public Guid GroupId { get; set; }
        public ProjectParticipantRole Role { get; set; }
        public ProjectParticipantStatus Status { get; set; }
    }
}
