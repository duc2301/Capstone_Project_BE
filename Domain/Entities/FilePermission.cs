using Domain.Enum.Permission;
using Domain.Enum.Project;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    // ACL trên file. Mỗi dòng gán cho ĐÚNG MỘT chủ thể:
    //   - Nhóm (ProjectParticipantId): override quyền nhóm cho file (đè quyền thư mục); hoặc
    //   - Tài khoản (AccountId): override riêng theo người dùng (kiểu Google Drive) — cấp/chặn một
    //     user cụ thể trên file này. Ràng buộc CHECK: đúng một trong hai cột được đặt.
    public class FilePermission : IPermissionAcl
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public Guid? ProjectParticipantId { get; set; }
        public Guid? AccountId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }       // Sửa
        //public bool CanUpdate { get; set; }     // Cập nhật (upload phiên bản)
        //public bool CanDownload { get; set; }   // Tải về
        //public bool CanVerify { get; set; }     // Thẩm tra
        public bool CanApprove { get; set; }    // Duyệt

        public PermissionStatus Status { get; set; }

        public FileItem FileItem { get; set; } = null!;
        public ProjectParticipant? ProjectParticipant { get; set; }
        public Account? Account { get; set; }
    }
}
