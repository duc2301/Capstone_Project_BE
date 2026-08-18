namespace Domain.Enum.Cde
{
    public static class CdeAreaExtensions
    {
        /// <summary>
        /// Vùng chính thức: nội dung đã được phê duyệt/phát hành (Published) hoặc đã niêm phong
        /// lưu trữ (Archived). Đây cũng là điều kiện để một tệp được đưa vào chỉ mục ngữ nghĩa —
        /// định nghĩa nằm ở đúng một chỗ này để mọi luồng ghi/đọc chỉ mục không lệch nhau.
        /// </summary>
        public static bool IsOfficialArea(this CdeArea area)
            => area is CdeArea.Published or CdeArea.Archived;
    }
}
