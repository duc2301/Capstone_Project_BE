namespace Domain.Entities
{
    // Thư mục liên kết trong RFI/Issue (nút "Mở file" -> file trong link đã liên kết)
    public class IssueCitedFolder
    {
        public Guid Id { get; set; }
        public Guid IssueId { get; set; }
        public Guid FolderId { get; set; }

        public Issue Issue { get; set; } = null!;
        public Folder Folder { get; set; } = null!;
    }
}
