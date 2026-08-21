using Domain.Enum.Permission;

namespace Domain.Entities
{
    /// <summary>
    /// Cấp quyền XEM một file cho MỘT tài khoản cụ thể (không theo nhóm). Sinh ra khi account được
    /// chỉ định ký ở luồng Shared -> Published: người ký cần xem được file để ký, kể cả khi nhóm của
    /// họ không được cấp CanView trên folder chứa file.
    ///
    /// Grant là một đường "Allow" CỘNG THÊM, độc lập với ACL nhóm (FolderPermission/FilePermission) —
    /// không phải override. Grant bị thu hồi (hard-delete) khi file RỜI khu vực Shared: chuyển sang
    /// Published (hoàn tất ký — xem ApprovalService.ApproveAsync) hoặc trả về WIP (xem
    /// ZoneReturnRequestService). Bị từ chối duyệt mà file VẪN Ở Shared thì KHÔNG thu hồi (chỉ khi
    /// thực sự trả về WIP).
    /// </summary>
    public class FileViewGrant
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public Guid AccountId { get; set; }

        // Approval request đã assign người này ký -> phục vụ truy vết nguồn gốc grant.
        public Guid? SourceApprovalRequestId { get; set; }

        public PermissionStatus Status { get; set; } = PermissionStatus.Active;
        public DateTime CreatedAt { get; set; }

        public FileItem FileItem { get; set; } = null!;
        public Account Account { get; set; } = null!;
        public ApprovalRequest? SourceApprovalRequest { get; set; }
    }
}
