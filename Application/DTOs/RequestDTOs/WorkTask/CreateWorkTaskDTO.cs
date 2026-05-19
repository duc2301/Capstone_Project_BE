using System.ComponentModel.DataAnnotations;
using Domain.Enum.Schedule;

namespace Application.DTOs.RequestDTOs.WorkTask
{
    public class CreateWorkTaskDTO
    {
        [Required]
        public Guid ScheduleId { get; set; }

        public Guid? ParentWorkTaskId { get; set; }

        [Required]
        [StringLength(100)]
        public string Code { get; set; } = null!;

        [Required]
        [StringLength(500)]
        public string Name { get; set; } = null!;

        public int Level { get; set; }
        public string? Unit { get; set; }

        public decimal? PlannedProduction { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }

        [Required]
        public WorkTaskStatus Status { get; set; }
    }
}
