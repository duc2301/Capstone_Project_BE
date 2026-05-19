using System.ComponentModel.DataAnnotations;
using Domain.Enum.Schedule;

namespace Application.DTOs.RequestDTOs.Schedule
{
    public class UpdateScheduleDTO
    {
        [StringLength(250)]
        public string? Name { get; set; }

        public Guid? SourceFileVersionId { get; set; }
        public int? Version { get; set; }
        public ScheduleStatus? Status { get; set; }
    }
}
