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
        public DateTime? CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public ICollection<PackageAssignment> Assignments { get; set; } = new List<PackageAssignment>();
    }
}
