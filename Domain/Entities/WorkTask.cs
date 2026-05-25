using Domain.Enum.Schedule;

using Domain.Common;

namespace Domain.Entities
{
    // Công tác / hạng mục — cây theo "Phân cấp" (cấp 1/2/3) trong file tiến độ
    public class WorkTask : IEntity
    {
        public Guid Id { get; set; }
        public Guid ScheduleId { get; set; }
        public Guid? ParentWorkTaskId { get; set; }
        public string Code { get; set; } = null!;        // STT mã hiệu
        public string Name { get; set; } = null!;
        public int Level { get; set; }                    // Phân cấp 1/2/3
        public string? Unit { get; set; }

        public decimal? PlannedProduction { get; set; }   // sản lượng dự kiến
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }

        public decimal? ActualProduction { get; set; }    // sản lượng thực tế (lũy kế)
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public decimal? PercentComplete { get; set; }

        public WorkTaskStatus Status { get; set; }
        public bool IsLocked { get; set; }                // đã báo cáo -> khóa, ko sửa tên/thời gian

        public Schedule Schedule { get; set; } = null!;
        public WorkTask? ParentWorkTask { get; set; }
        public ICollection<WorkTask> ChildWorkTasks { get; set; } = new List<WorkTask>();
        public ICollection<WorkTaskModelLink> ModelLinks { get; set; } = new List<WorkTaskModelLink>();
        public ICollection<WorkTaskPermission> Permissions { get; set; } = new List<WorkTaskPermission>();
    }
}
