namespace Domain.Entities
{
    // Field KHÔNG bắt buộc của convention được BẬT áp dụng thêm cho 1 folder cụ thể.
    // Có row = folder này áp dụng field đó. Field bắt buộc/khóa luôn áp dụng, không cần row.
    public class FolderNamingField
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public Guid NamingConventionFieldId { get; set; }
        public Guid? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Folder Folder { get; set; } = null!;
        public NamingConventionField Field { get; set; } = null!;
    }
}
