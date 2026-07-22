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
        public Guid OrganizationTypeId { get; set; }     // FK -> bảng lookup OrganizationType
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsJointVenture { get; set; }
        public Guid? RepresentativeOrganizationId { get; set; }
        
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public OrganizationType OrganizationType { get; set; } = null!;
        public ICollection<PackageAssignment> PackageAssignments { get; set; } = new List<PackageAssignment>();
    }
}
