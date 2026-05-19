using System.ComponentModel.DataAnnotations;
using Domain.Enum.File;

namespace Application.DTOs.RequestDTOs.FileItem
{
    public class CreateFileItemDTO
    {
        [Required]
        public Guid FolderId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        [Required]
        public FileType FileType { get; set; }

        public Guid? CurrentVersionId { get; set; }
    }
}
