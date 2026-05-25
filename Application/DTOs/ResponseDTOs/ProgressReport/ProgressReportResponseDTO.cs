using Domain.Enum.Schedule;

namespace Application.DTOs.ResponseDTOs.ProgressReport
{
    public class ProgressReportResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ScheduleId { get; set; }
        public ProgressReportType ReportType { get; set; }
        public Guid? ReportedByOrganizationId { get; set; }
        public DateTime? ReportDate { get; set; }
        public ProgressReportStatus Status { get; set; }
        public Guid? ApprovedByAccountId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
