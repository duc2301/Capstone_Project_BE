namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class RelatedFilesResponseDTO
    {
        public bool CanLink { get; set; }
        public List<RelatedFileDTO> Files { get; set; } = new();
    }
}
