using Domain.Enum.Permission;

namespace Domain.Entities
{
    // ACL trên thư mục. Mỗi dòng gán cho ĐÚNG MỘT chủ thể:
    //   - Nhóm (ProjectParticipantId): quyền nhóm bình thường; hoặc
    //   - Tài khoản (AccountId): override riêng theo người dùng (kiểu Google Drive) — đè quyền nhóm,
    //     dùng để cấp/chặn một user cụ thể. Ràng buộc CHECK: đúng một trong hai cột được đặt.
    // 5 cờ trong tài liệu + Xem ngầm định. Workflow phiếu yêu cầu suy quyền từ đây.
    public class FolderPermission : IPermissionAcl
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public Guid? ProjectParticipantId { get; set; }
        public Guid? AccountId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }       // Sửa
        //public bool CanUpdate { get; set; }     // Cập nhật (upload phiên bản)
        //public bool CanDownload { get; set; }   // Tải về
        //public bool CanVerify { get; set; }     // Thẩm tra
        public bool CanApprove { get; set; }    // Duyệt

        public PermissionStatus Status { get; set; }

        public Folder Folder { get; set; } = null!;
        public ProjectParticipant? ProjectParticipant { get; set; }
        public Account? Account { get; set; }
    }
}
