using System.ComponentModel.DataAnnotations;
using Domain.Enum.Schedule;

namespace Application.DTOs.RequestDTOs.WorkTask
{
    public class UpdateWorkTaskDTO
    {
        [StringLength(100)]
        public string? Code { get; set; }

        [StringLength(500)]
        public string? Name { get; set; }

        public int? Level { get; set; }
        public string? Unit { get; set; }

        public decimal? PlannedProduction { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }

        public decimal? ActualProduction { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public decimal? PercentComplete { get; set; }

        public WorkTaskStatus? Status { get; set; }
        public bool? IsLocked { get; set; }
    }
}
