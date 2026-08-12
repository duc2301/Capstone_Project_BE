namespace Domain.Enum.Permission
{
    // Mức quyền hiển thị trên ma trận phân quyền (RACI). Một ô = một mức.
    // Ánh xạ sang các cờ trên Folder/FilePermission qua PermissionLevelMapper.
    public enum PermissionLevel
    {
        // Kế thừa: không có ý kiến ở cấp này → suy quyền từ thư mục chứa (chỉ áp dụng cho file).
        Inherit,

        // N: không có quyền. File → dòng Active chặn (đè quyền thư mục); thư mục → dòng Inactive.
        NoAccess,

        // R: chỉ đọc (CanView).
        Read,

        // W: đọc + sửa (CanView + CanEdit).
        Write
    }
}
