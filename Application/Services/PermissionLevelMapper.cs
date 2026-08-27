using Domain.Entities;
using Domain.Enum.Permission;

namespace Application.Services
{
    /// <summary>
    /// Nguồn chân lý duy nhất cho việc ánh xạ mức N/R/W/Inherit lên các cờ của một dòng ACL
    /// (Folder/FilePermission), và ngược lại. Mọi nơi ghi quyền phải đi qua đây để "không có quyền"
    /// không mang hai ý nghĩa khác nhau giữa các màn hình. PermissionCheckingService đọc đúng theo
    /// hợp đồng này nên đường đọc không cần đổi.
    ///
    /// Hợp đồng lưu trữ:
    ///   Inherit  -> Status=Inactive (hoặc không có dòng)            // chỉ file: suy quyền từ thư mục
    ///   N (file) -> Status=Active,  View=false, Edit=false          // dòng chặn, đè quyền thư mục
    ///   N (folder)-> Status=Inactive                                // thư mục phẳng: không quyền = Inactive
    ///   R        -> Status=Active,  View=true,  Edit=false
    ///   W        -> Status=Active,  View=true,  Edit=true
    /// CanApprove giữ nguyên hành vi hiện tại: true với mọi dòng cấp quyền (R/W), false với N/Inherit.
    /// </summary>
    public static class PermissionLevelMapper
    {
        /// <summary>Ánh xạ mức quyền lên một dòng ACL. isFile quyết định cách lưu N (xem hợp đồng trên).</summary>
        public static void Apply(IPermissionAcl acl, PermissionLevel level, bool isFile)
        {
            switch (level)
            {
                case PermissionLevel.Write:
                    Set(acl, PermissionStatus.Active, canView: true, canEdit: true, canApprove: true);
                    break;

                case PermissionLevel.Read:
                    Set(acl, PermissionStatus.Active, canView: true, canEdit: false, canApprove: true);
                    break;

                case PermissionLevel.NoAccess:
                    // File: dòng Active chặn để đè quyền thư mục. Thư mục (phẳng): Inactive là đủ.
                    if (isFile)
                        Set(acl, PermissionStatus.Active, canView: false, canEdit: false, canApprove: true);
                    else
                        Set(acl, PermissionStatus.Inactive, canView: false, canEdit: false, canApprove: true);
                    break;

                case PermissionLevel.Inherit:
                default:
                    Set(acl, PermissionStatus.Inactive, canView: false, canEdit: false, canApprove: true);
                    break;
            }
        }

        /// <summary>Quy đổi cặp cờ (CanView, CanEdit) từ input cũ sang mức quyền.</summary>
        public static PermissionLevel FromFlags(bool canView, bool canEdit)
            => canEdit ? PermissionLevel.Write
             : canView ? PermissionLevel.Read
             : PermissionLevel.NoAccess;

        /// <summary>
        /// Đọc mức quyền hiện tại từ một dòng ACL (dùng để dựng ô trên ma trận).
        /// Dòng Inactive/không tồn tại = Inherit; Active có View = R/W; Active không View = N.
        /// </summary>
        public static PermissionLevel ToLevel(IPermissionAcl? acl)
        {
            if (acl == null || acl.Status != PermissionStatus.Active)
                return PermissionLevel.Inherit;

            if (!acl.CanView)
                return PermissionLevel.NoAccess;

            return acl.CanEdit ? PermissionLevel.Write : PermissionLevel.Read;
        }

        private static void Set(IPermissionAcl acl, PermissionStatus status, bool canView, bool canEdit, bool canApprove)
        {
            acl.Status = status;
            acl.CanView = canView;
            acl.CanEdit = canEdit;
            acl.CanApprove = canApprove;
        }
    }
}
