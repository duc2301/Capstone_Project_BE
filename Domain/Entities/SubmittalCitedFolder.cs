namespace Domain.Entities
{
    // Thư mục được trích dẫn trong phiếu -> nguồn suy quyền trình nộp/thẩm tra/duyệt
    public class SubmittalCitedFolder
    {
        public Guid Id { get; set; }
        public Guid SubmittalId { get; set; }
        public Guid FolderId { get; set; }

        public Submittal Submittal { get; set; } = null!;
        public Folder Folder { get; set; } = null!;
    }
}
