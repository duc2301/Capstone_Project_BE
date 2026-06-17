using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Approval
{
    /// <summary>
    /// Dữ liệu gửi lên khi từ chối phê duyệt file.
    /// </summary>
    public class RejectApprovalRequestDTO
    {
        /// <summary>
        /// Lý do từ chối. Bắt buộc nhập.
        /// </summary>
        [Required]
        [MinLength(1)]
        public string Reason { get; set; } = null!;
    }
}
