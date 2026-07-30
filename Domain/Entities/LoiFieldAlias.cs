namespace Domain.Entities
{
    // Quy tên tham số trong model về tên chuẩn của Phụ lục 02.
    public class LoiFieldAlias
    {
        public Guid Id { get; set; }

        // Tên tham số chuẩn, đã chuẩn hoá (vd "khoi tich").
        public string FieldNameNormalized { get; set; } = null!;

        // Tên gặp trong file IFC, đã chuẩn hoá (vd "the tich thiet ke").
        public string AliasNormalized { get; set; } = null!;

        // null = dùng chung toàn hệ thống; có giá trị = riêng một dự án.
        public Guid? ProjectId { get; set; }

        public Guid? CreatedByAccountId { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
