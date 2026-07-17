using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.FileItem
{
    public class LinkRelatedFilesDTO
    {
        [Required]
        [MinLength(1, ErrorMessage = "Phải chọn ít nhất một tệp để liên kết.")]
        public List<Guid> RelatedFileItemIds { get; set; } = new();
    }
}
