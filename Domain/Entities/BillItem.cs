using Domain.Enum.Contract;

namespace Domain.Entities
{
    // Dòng bill thầu — cây theo "Phân cấp". Cấp cha = hạng mục, cấp lá = công tác.
    public class BillItem
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public Guid? ContractAppendixId { get; set; }   // phụ lục đưa vào / điều chỉnh dòng này
        public Guid? ParentBillItemId { get; set; }
        public string Code { get; set; } = null!;        // STT mã hiệu
        public int Level { get; set; }                    // Phân cấp
        public string Name { get; set; } = null!;
        public string? Unit { get; set; }

        public decimal? ContractQuantity { get; set; }    // khối lượng theo hợp đồng
        public decimal? ContractUnitPrice { get; set; }   // đơn giá theo hợp đồng
        public decimal? ContractAmount { get; set; }      // thành tiền theo hợp đồng

        public decimal? AdjustedQuantity { get; set; }    // điều chỉnh / phát sinh tăng giảm
        public decimal? AdjustedUnitPrice { get; set; }
        public decimal? AdjustedAmount { get; set; }

        public BillSheet Sheet { get; set; }              // trong / ngoài hợp đồng (Sheet A/B)

        public Contract Contract { get; set; } = null!;
        public BillItem? ParentBillItem { get; set; }
        public ICollection<BillItem> ChildBillItems { get; set; } = new List<BillItem>();
    }
}
