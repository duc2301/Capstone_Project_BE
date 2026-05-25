using Domain.Enum.Schedule;

namespace Application.DTOs.ResponseDTOs.Schedule
{
    public class ScheduleResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ContractPackageId { get; set; }
        public string Name { get; set; } = null!;
        public Guid? SourceFileVersionId { get; set; }
        public int Version { get; set; }
        public ScheduleStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
