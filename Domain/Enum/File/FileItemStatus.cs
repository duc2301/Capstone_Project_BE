namespace Domain.Enum.File
{
    /// <summary>
    /// Trạng thái phê duyệt của file CDE.
    /// </summary>
    public enum FileItemStatus
    {
        /// <summary>File mới tạo, chưa gửi duyệt.</summary>
        Draft,

        /// <summary>File đã gửi duyệt và đang chờ xử lý.</summary>
        PendingApproval,

        /// <summary>File đã được phê duyệt.</summary>
        Approved,

        /// <summary>File đã bị từ chối.</summary>
        Rejected
    }
}
