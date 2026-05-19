namespace Domain.Enum.Schedule
{
    public enum ProgressReportStatus
    {
        Pending,    // chờ tư vấn giám sát duyệt
        Approved,   // đã duyệt -> ghi nhận sản lượng
        Rejected
    }
}
