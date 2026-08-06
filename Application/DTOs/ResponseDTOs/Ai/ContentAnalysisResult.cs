namespace Application.DTOs.ResponseDTOs.Ai
{
    // Kết quả AI phân tích 1 file: tóm tắt nội dung + cờ nghi ngờ nội dung không liên quan (cần người kiểm tra).
    public class ContentAnalysisResult
    {
        // Tóm tắt tiếng Việt cho người dùng đọc nhanh (null nếu không tóm tắt được).
        public string? Summary { get; set; }

        // true khi nội dung có dấu hiệu KHÔNG liên quan tới tên tệp / không phải tài liệu dự án.
        public bool Suspicious { get; set; }

        // Lý do ngắn khi Suspicious = true (null/empty khi không nghi ngờ).
        public string? Reason { get; set; }
    }
}
