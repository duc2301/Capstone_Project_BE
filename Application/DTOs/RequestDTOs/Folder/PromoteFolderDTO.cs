using System.ComponentModel.DataAnnotations;
using Domain.Enum.Cde;

namespace Application.DTOs.RequestDTOs.Folder
{
    // Chuyển trạng thái cả thư mục (đệ quy) sang khu vực kế tiếp.
    // Chỉ được tiến đúng 1 bậc: Wip→Shared→Published→Archived.
    public class PromoteFolderDTO
    {
        [Required]
        public CdeArea TargetArea { get; set; }
    }
}
