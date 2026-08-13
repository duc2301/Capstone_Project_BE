using Domain.Enum.Permission;

namespace Domain.Entities
{
    // Bộ cờ ACL chung của FolderPermission và FilePermission, cho phép PermissionLevelMapper
    // ánh xạ mức N/R/W/Inherit lên cả hai loại quyền mà không lặp code.
    public interface IPermissionAcl
    {
        bool CanView { get; set; }
        bool CanEdit { get; set; }
        bool CanApprove { get; set; }
        PermissionStatus Status { get; set; }
    }
}
