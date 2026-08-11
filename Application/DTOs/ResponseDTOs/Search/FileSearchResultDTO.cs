namespace Application.DTOs.ResponseDTOs.Search
{
    public class FileSearchResultDTO
    {
        public Guid FileItemId { get; set; }
        public Guid FolderId { get; set; }
        public string FileName { get; set; } = null!;
        public string Snippet { get; set; } = null!;
        public double Similarity { get; set; }
        public int MatchCount { get; set; }

        public string Area { get; set; } = null!;
        public bool IsUnderRevision { get; set; }
    }
}