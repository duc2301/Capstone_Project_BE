using Domain.Enum.ContractPackage;

using Domain.Common;

namespace Domain.Entities
{
    // Gói thầu thuộc 1 dự án
    public class ContractPackage : IEntity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal? ContractValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public PackageStatus Status { get; set; }
        public bool IsDefault { get; set; }

        public string? WorkTypes { get; set; }        // Lưu trữ dạng JSON array các loại công việc
        public string? ScopeDescription { get; set; } // Phạm vi và khối lượng
        public decimal? TaxRate { get; set; }         // % Thuế VAT (ví dụ: 8, 10)
        public string? Currency { get; set; }         // Loại tiền tệ (VD: "VND", "USD")
        public string? Notes { get; set; }            // Ghi chú kỹ thuật bổ sung

        public Guid? DocumentFolderId { get; set; }     // File đính kèm gói thầu (nằm trong CDE)

        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<PackageAssignment> Assignments { get; set; } = new List<PackageAssignment>();
    }
}
