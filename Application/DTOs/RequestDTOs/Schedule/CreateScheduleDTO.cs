using System.ComponentModel.DataAnnotations;
using Domain.Enum.Schedule;

namespace Application.DTOs.RequestDTOs.Schedule
{
    public class CreateScheduleDTO
    {
        [Required]
        public Guid ContractPackageId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        public Guid? SourceFileVersionId { get; set; }
        public int Version { get; set; }

        [Required]
        public ScheduleStatus Status { get; set; }
    }
}
