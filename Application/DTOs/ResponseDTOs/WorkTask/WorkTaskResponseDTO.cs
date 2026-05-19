using Domain.Enum.Schedule;

namespace Application.DTOs.ResponseDTOs.WorkTask
{
    public class WorkTaskResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ScheduleId { get; set; }
        public Guid? ParentWorkTaskId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Level { get; set; }
        public string? Unit { get; set; }
        public decimal? PlannedProduction { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public decimal? ActualProduction { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public decimal? PercentComplete { get; set; }
        public WorkTaskStatus Status { get; set; }
        public bool IsLocked { get; set; }
    }
}
