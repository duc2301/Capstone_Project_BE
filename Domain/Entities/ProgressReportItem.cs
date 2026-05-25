namespace Domain.Entities
{
    // Sản lượng thực tế báo cáo cho từng công tác trong 1 báo cáo
    public class ProgressReportItem
    {
        public Guid Id { get; set; }
        public Guid ProgressReportId { get; set; }
        public Guid WorkTaskId { get; set; }
        public decimal? ActualProduction { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string? Note { get; set; }

        public ProgressReport ProgressReport { get; set; } = null!;
        public WorkTask WorkTask { get; set; } = null!;
    }
}
