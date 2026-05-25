namespace Domain.Enum.Schedule
{
    public enum WorkTaskStatus
    {
        NotStarted,   // chưa thi công (mô hình màu xám)
        InProgress,   // đang thi công (màu xanh)
        Done,         // đã thi công xong (màu đỏ)
        Exceeded      // xong nhưng vượt sản lượng dự kiến
    }
}
