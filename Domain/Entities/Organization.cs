using Domain.Enum.Department;

using Domain.Common;

namespace Domain.Entities
{
    // Tổ chức bên ngoài tham gia dự án (CĐT, nhà thầu, tư vấn...),
    // thêm bằng mã số thuế rồi tra cứu. Khác với Department (phòng ban nội bộ).
    public class Organization : IEntity, IAuditable
    {
        public Guid Id { get; set; }
        public string TaxCode { get; set; } = null!;
        public string LegalName { get; set; } = null!;   // tên đăng ký với nhà nước
        public string? DisplayName { get; set; }
        public DepartmentType Type { get; set; }          // tái dùng taxonomy bên liên quan
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ProjectParticipant> ProjectParticipations { get; set; } = new List<ProjectParticipant>();
        public ICollection<PackageAssignment> PackageAssignments { get; set; } = new List<PackageAssignment>();
    }
}
