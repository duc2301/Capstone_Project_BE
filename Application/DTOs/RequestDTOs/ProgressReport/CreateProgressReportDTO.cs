using System.ComponentModel.DataAnnotations;
using Domain.Enum.Schedule;

namespace Application.DTOs.RequestDTOs.ProgressReport
{
    public class CreateProgressReportDTO
    {
        [Required]
        public Guid ScheduleId { get; set; }

        [Required]
        public ProgressReportType ReportType { get; set; }

        public Guid? ReportedByOrganizationId { get; set; }
        public DateTime? ReportDate { get; set; }

        [Required]
        public ProgressReportStatus Status { get; set; }
    }
}
