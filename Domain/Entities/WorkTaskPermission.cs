namespace Domain.Entities
{
    // Phân quyền báo cáo theo nhóm cho 1 công tác.
    // Mỗi nhóm chỉ được chọn 1 lần; sau khi báo cáo thì khóa, không đổi.
    public class WorkTaskPermission
    {
        public Guid Id { get; set; }
        public Guid WorkTaskId { get; set; }
        public Guid GroupId { get; set; }

        public bool CanReport { get; set; }                  // Báo cáo
        public bool CanRenameTask { get; set; }              // Cập nhật tên công việc
        public bool CanUpdatePlannedProduction { get; set; } // Cập nhật sản lượng dự kiến
        public bool CanUpdateDependency { get; set; }        // Cập nhật phụ thuộc
        public bool CanAssignPermission { get; set; }        // Phân quyền
        public bool IsLocked { get; set; }

        public WorkTask WorkTask { get; set; } = null!;
        public Group Group { get; set; } = null!;
    }
}
