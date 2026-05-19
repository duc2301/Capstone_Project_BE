using Domain.Enum.Schedule;

using Domain.Common;

namespace Domain.Entities
{
    // Tiến độ thi công của 1 gói thầu (nguồn từ file MS Project upload qua CDE)
    public class Schedule : IEntity, IAuditable
    {
        public Guid Id { get; set; }
        public Guid ContractPackageId { get; set; }
        public string Name { get; set; } = null!;
        public Guid? SourceFileVersionId { get; set; }   // file MSProject trong kho CDE
        public int Version { get; set; }                 // lần cập nhật tiến độ
        public ScheduleStatus Status { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ContractPackage ContractPackage { get; set; } = null!;
        public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
        public ICollection<ProgressReport> Reports { get; set; } = new List<ProgressReport>();
    }
}
