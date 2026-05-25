using Domain.Enum.Contract;

using Domain.Common;

namespace Domain.Entities
{
    // Hợp đồng (bill thầu) ký giữa chủ đầu tư và nhà thầu, theo gói thầu
    public class Contract : IEntity, IAuditable
    {
        public Guid Id { get; set; }
        public Guid ContractPackageId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public Guid? ContractorOrganizationId { get; set; }
        public Guid? SourceFileVersionId { get; set; }   // file excel bill thầu trong CDE
        public DateTime? SignedDate { get; set; }
        public ContractStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ContractPackage ContractPackage { get; set; } = null!;
        public ICollection<ContractAppendix> Appendices { get; set; } = new List<ContractAppendix>();
        public ICollection<BillItem> BillItems { get; set; } = new List<BillItem>();
    }
}
