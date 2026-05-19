namespace Domain.Enum.Submittal
{
    public enum SubmittalStatus
    {
        Draft,
        Submitted,      // Đã trình nộp
        UnderReview,    // Đang thẩm tra
        Verified,       // Đã thẩm tra
        Approved,       // Đã duyệt
        Rejected,       // Từ chối
        Returned        // Trả lại để chỉnh sửa
    }
}
