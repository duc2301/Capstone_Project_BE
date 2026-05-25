using Domain.Enum.Schedule;

namespace Application.DTOs.RequestDTOs.ProgressReport
{
    public class UpdateProgressReportDTO
    {
        public ProgressReportType? ReportType { get; set; }
        public DateTime? ReportDate { get; set; }
        public ProgressReportStatus? Status { get; set; }
        public Guid? ApprovedByAccountId { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
