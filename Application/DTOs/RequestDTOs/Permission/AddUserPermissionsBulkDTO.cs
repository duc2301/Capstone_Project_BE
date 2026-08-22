using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Permission
{
    // Payload cho hộp thoại "Phân quyền" theo NGƯỜI DÙNG (kiểu Google Drive), dùng chung file/folder.
    //   - UsersPermission: thêm/sửa override cho từng tài khoản. CanView=false => dòng CHẶN (đè quyền nhóm).
    //   - RemoveAccountIds: gỡ override => tài khoản trở lại kế thừa quyền nhóm.
    // Cho phép chỉ-remove hoặc chỉ-thêm (khác DTO nhóm có MinLength(1)); service kiểm tra "rỗng cả hai".
    public class AddUserPermissionsBulkDTO
    {
        public Guid Id { get; set; }   // fileItemId hoặc folderId

        public List<AddUserPermissionDTO> UsersPermission { get; set; } = new();

        public List<Guid> RemoveAccountIds { get; set; } = new();
    }

    public class AddUserPermissionDTO
    {
        [Required]
        public Guid AccountId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }       // Sửa
    }
}
