using System.ComponentModel.DataAnnotations;
using Domain.Enum.File;

namespace Application.DTOs.RequestDTOs.FileItem
{
    // Lệnh upload (phần metadata) — nội dung file truyền riêng dưới dạng Stream.
    public class UploadFileDTO
    {
        [Required]
        public Guid FolderId { get; set; }

        [Required]
        public FileType FileType { get; set; }

        // Tên logic của tài liệu (không kèm đuôi). Bỏ trống -> lấy theo tên file gốc.
        [StringLength(200)]
        public string? Name { get; set; }

        public List<Guid>? RelatedFileItemIds { get; set; }
    }
}
