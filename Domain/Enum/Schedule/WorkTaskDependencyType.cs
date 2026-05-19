namespace Domain.Enum.Schedule
{
    // Kiểu phụ thuộc như MS Project
    public enum WorkTaskDependencyType
    {
        FinishToStart,
        StartToStart,
        FinishToFinish,
        StartToFinish
    }
}
