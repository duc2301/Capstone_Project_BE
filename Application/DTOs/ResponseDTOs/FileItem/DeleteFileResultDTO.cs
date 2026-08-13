namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class DeleteFileResultDTO
    {
        public Guid FileItemId { get; set; }
        public bool FileRemoved { get; set; }
        public string DeletedVersion { get; set; } = null!;
        public string? CurrentVersion { get; set; }
    }
}
