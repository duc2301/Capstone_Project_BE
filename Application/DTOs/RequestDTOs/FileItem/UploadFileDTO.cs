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
        // Bị BỎ QUA nếu folder có naming convention (tên do backend tự sinh).
        [StringLength(200)]
        public string? Name { get; set; }

        // Lựa chọn dropdown khi folder có naming convention — JSON array:
        // [{"fieldId":"...","valueId":"..."}]. Field bị khóa không cần gửi.
        public string? NamingSelections { get; set; }
    }
}
