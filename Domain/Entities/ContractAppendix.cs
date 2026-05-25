namespace Domain.Entities
{
    // Phụ lục hợp đồng — cập nhật khối lượng/đơn giá khi phát sinh tăng giảm
    public class ContractAppendix
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public int AppendixNo { get; set; }
        public Guid? SourceFileVersionId { get; set; }
        public DateTime? SignedDate { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Contract Contract { get; set; } = null!;
    }
}
