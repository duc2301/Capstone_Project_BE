using System.ComponentModel.DataAnnotations;
using Domain.Enum.File;

namespace Application.DTOs.RequestDTOs.FileItem
{
    public class UpdateFileItemDTO
    {
        [StringLength(250)]
        public string? Name { get; set; }

        public FileType? FileType { get; set; }
        public Guid? CurrentVersionId { get; set; }
    }
}
