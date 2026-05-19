namespace Domain.Entities
{
    // Ghi chú trên file (cần đúng quyền mới được ghi chú)
    public class FileNote
    {
        public Guid Id { get; set; }
        public Guid FileVersionId { get; set; }
        public int? PageNumber { get; set; }
        public string? CoordinateJson { get; set; }   // vị trí ghi chú trên trang/mô hình
        public string Content { get; set; } = null!;
        public Guid? AuthorAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public FileVersion FileVersion { get; set; } = null!;
    }
}
