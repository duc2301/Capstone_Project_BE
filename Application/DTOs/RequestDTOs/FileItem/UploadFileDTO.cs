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

        // "Tệp liên quan" chọn kèm lúc upload — TÙY CHỌN (bỏ trống = không liên kết gì).
        // Chỉ nhận file nằm trong ô của nhóm sở hữu ở cùng khu vực với FolderId và người upload
        // có quyền View; id không hợp lệ sẽ bị FileLinkService từ chối.
        public List<Guid>? RelatedFileItemIds { get; set; }

        public string? NamingSelections { get; set; }

        public bool BypassNamingConvention { get; set; }

        // Trùng tên trong thư mục đích thì làm gì. Bỏ trống = chưa hỏi người dùng -> BE từ chối (409)
        // chứ KHÔNG tự lên phiên bản: đè lên tài liệu của người khác phải là một lựa chọn có ý thức.
        public UploadDuplicateAction DuplicateAction { get; set; } = UploadDuplicateAction.None;
    }
}
