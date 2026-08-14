using Domain.Enum.Permission;

namespace Domain.Entities
{
    /// <summary>
    /// Đường "Allow" CỘNG THÊM: cho phép người được giao issue (hoặc ẢNH CHỤP thành viên nhóm được giao)
    /// XEM file được liên kết của issue, kể cả khi nhóm của họ không có CanView trên folder chứa file.
    /// Độc lập với ACL nhóm VÀ với FileViewGrant của luồng ký (bảng riêng -> không đụng unique index).
    /// Bị thu hồi (hard-delete) khi: (1) issue được resolve (IssueService, theo IssueId — không đụng grant
    /// của issue khác/luồng ký), (2) file liên kết chuyển sang Published (ApprovalService.ApproveAsync),
    /// hoặc (3) file liên kết trả về WIP (ZoneReturnRequestService) — (2)(3) theo FileItemId. Cấp cho issue
    /// trên file ở cả Shared lẫn Published. Là ảnh chụp lúc tạo issue: reassignment / người vào nhóm sau
    /// không được cấp (ngoài phạm vi hiện tại).
    /// </summary>
    public class IssueFileViewGrant
    {
        public Guid Id { get; set; }
        public Guid IssueId { get; set; }
        public Guid FileItemId { get; set; }
        public Guid AccountId { get; set; }
        public PermissionStatus Status { get; set; } = PermissionStatus.Active;
        public DateTime CreatedAt { get; set; }

        public Issue Issue { get; set; } = null!;
        public FileItem FileItem { get; set; } = null!;
        public Account Account { get; set; } = null!;
    }
}
