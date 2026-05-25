using Domain.Enum.Schedule;

using Domain.Common;

namespace Domain.Entities
{
    // Báo cáo công trường/định kỳ/ngày -> tư vấn giám sát duyệt -> ghi nhận sản lượng
    public class ProgressReport : IEntity
    {
        public Guid Id { get; set; }
        public Guid ScheduleId { get; set; }
        public ProgressReportType ReportType { get; set; }
        public Guid? ReportedByOrganizationId { get; set; }
        public DateTime? ReportDate { get; set; }
        public ProgressReportStatus Status { get; set; }
        public Guid? ApprovedByAccountId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Schedule Schedule { get; set; } = null!;
        public ICollection<ProgressReportItem> Items { get; set; } = new List<ProgressReportItem>();
    }
}
