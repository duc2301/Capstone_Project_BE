using Domain.Enum.Schedule;

namespace Domain.Entities
{
    // Phụ thuộc giữa các công tác (như MS Project). Guid thuần để tránh
    // EF nhập nhằng 2 FK cùng trỏ WorkTask.
    public class WorkTaskDependency
    {
        public Guid Id { get; set; }
        public Guid PredecessorWorkTaskId { get; set; }
        public Guid SuccessorWorkTaskId { get; set; }
        public WorkTaskDependencyType Type { get; set; }
    }
}
