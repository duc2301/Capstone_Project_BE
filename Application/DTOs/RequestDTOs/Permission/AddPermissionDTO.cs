using Domain.Enum.Permission;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs.RequestDTOs.Permission
{
    public class AddPermissionDTO
    {
        [Required]
        public Guid ProjectParticipantId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }       // Sửa
        public bool CanUpdate { get; set; }     // Cập nhật (upload phiên bản)
        public bool CanDownload { get; set; }   // Tải về
        public bool CanVerify { get; set; }     // Thẩm tra
        public bool CanApprove { get; set; }    // Duyệt
    }
}
