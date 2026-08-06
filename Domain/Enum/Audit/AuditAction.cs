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

        Login,
        Logout,
        LoginFailed
    }
}
