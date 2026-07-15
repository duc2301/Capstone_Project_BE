namespace Application.DTOs.ResponseDTOs.Search
{
    public class FileSearchResultDTO
    {
        public Guid FileItemId { get; set; }
        // FE cần FolderId để mở trang "Xem chi tiết" đúng ngữ cảnh thư mục.
        public Guid FolderId { get; set; }
        public string FileName { get; set; } = null!;
        public string Snippet { get; set; } = null!;
        public double Similarity { get; set; }
        public int MatchCount { get; set; }
    }
}