namespace Domain.Enum.Audit
{
    // CHỈ THÊM VÀO CUỐI — không chèn/đổi thứ tự, tránh lệch số integer đã lưu trong DB.
    public enum AuditAction
    {
        Create,
        Update,
        Delete,
        Move,
        Submit,
        Verify,
        Approve,
        Reject,
        Download,
        PermissionChange,

        // ===== bổ sung cho các luồng CDE =====
        Upload,
        NewVersion,
        Sign,
        ZoneTransfer,
        ReturnRequest,
        Invite,
        AcceptInvite,
        RejectInvite,
        Assign,
        StatusChange,
        Archive,        // Niêm phong lưu trữ: chốt bản Published chính thức vào vùng Archived

        // ===== theo dõi truy cập tài liệu =====
        View,           // Mở xem nội dung file (ghi có chống trùng, xem AuditLogService)
        Share,          // Cấp quyền xem file cho tài khoản cụ thể (FileViewGrant / IssueFileViewGrant)
        RevokeShare     // Thu hồi quyền xem đã cấp
    }
}
