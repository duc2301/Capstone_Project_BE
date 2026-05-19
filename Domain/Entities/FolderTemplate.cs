using Domain.Common;

namespace Domain.Entities
{
    // Thư mục mẫu: xuất/nhập cấu trúc thư mục dưới dạng JSON
    public class FolderTemplate : IEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string StructureJson { get; set; } = null!;
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
