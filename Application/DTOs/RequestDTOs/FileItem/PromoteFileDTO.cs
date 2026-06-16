using System.ComponentModel.DataAnnotations;
using Domain.Enum.Cde;

namespace Application.DTOs.RequestDTOs.FileItem
{
    // Chuyển trạng thái 1 tài liệu sang khu vực kế tiếp, có thể CHỌN version để chuyển.
    public class PromoteFileDTO
    {
        [Required]
        public CdeArea TargetArea { get; set; }

        // Bỏ trống = dùng version hiện hành của file.
        public Guid? VersionId { get; set; }
    }
}
