namespace Domain.Enum.File
{
    /// <summary>
    /// Trạng thái của yêu cầu phê duyệt file.
    /// </summary>
    public enum ApprovalRequestStatus
    {
        /// <summary>Yêu cầu đang chờ Team Leader xử lý.</summary>
        Pending,

        /// <summary>Yêu cầu đã được duyệt.</summary>
        Approved,

        /// <summary>Yêu cầu đã bị từ chối.</summary>
        Rejected
    }
}
