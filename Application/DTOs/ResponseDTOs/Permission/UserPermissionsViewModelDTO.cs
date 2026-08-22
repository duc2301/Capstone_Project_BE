using Domain.Enum.Permission;
using System;
using System.Collections.Generic;

namespace Application.DTOs.ResponseDTOs.Permission
{
    // Dual-list dữ liệu cho hộp thoại "Phân quyền" theo NGƯỜI DÙNG (kiểu Google Drive).
    // Dùng chung cho cả file và folder (cùng hình dạng, chỉ khác nguồn dữ liệu):
    //   - AvailableUsers (trái): user đang trong tầm nhìn nhóm của tài nguyên nhưng CHƯA có override.
    //   - SelectedUsers  (phải): user đã có override riêng (cấp thêm hoặc chặn) trên tài nguyên này.
    public class UserPermissionsViewModelDTO
    {
        public List<AccountItem> AvailableUsers { get; set; } = new();            // Left panel
        public List<UserPermissionResponseDTO> SelectedUsers { get; set; } = new(); // Right panel
    }

    public class AccountItem
    {
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class UserPermissionResponseDTO
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public PermissionStatus Status { get; set; }
    }
}
