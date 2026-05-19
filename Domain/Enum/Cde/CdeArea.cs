namespace Domain.Enum.Cde
{
    // 4 khu vực CDE theo ISO 19650 / TCVN 14177
    public enum CdeArea
    {
        Wip,        // Công việc trong quá trình - nội bộ từng đơn vị
        Shared,     // Chia sẻ - liên đơn vị, đang xem xét/đánh giá
        Published,  // Phát hành - thông tin chính thức đã phê duyệt
        Archived    // Lưu trữ - backup / phiên bản cũ
    }
}
