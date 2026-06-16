using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class FilePermission
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public Guid? ProjectParticipantId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }       // Sửa
        public bool CanUpdate { get; set; }     // Cập nhật (upload phiên bản)
        public bool CanDownload { get; set; }   // Tải về
        public bool CanVerify { get; set; }     // Thẩm tra
        public bool CanApprove { get; set; }    // Duyệt

        public bool InheritFromParent { get; set; }

        public FileItem FileItem { get; set; } = null!;
        public ProjectParticipant? ProjectParticipant { get; set; }
    }
}
