namespace Domain.Entities
{
    public class FileLink
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public Guid LinkedFileItemId { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public DateTime CreatedAt { get; set; }

        public FileItem FileItem { get; set; } = null!;
        public FileItem LinkedFileItem { get; set; } = null!;
    }
}
