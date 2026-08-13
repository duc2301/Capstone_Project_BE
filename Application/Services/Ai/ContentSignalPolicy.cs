namespace Application.Services.Ai
{
    // Quyết định một trích xuất văn bản có ĐỦ TÍN HIỆU để đem đi phân tích hay không.
    // Là luật nghiệp vụ ("thế nào là đủ chữ để tóm tắt"), không phải chi tiết của Ollama,
    // nên đặt ở Application; adapter AI chỉ gọi.
    //
    // Dưới ngưỡng thì LLM không còn gì để tóm tắt: nó sẽ lấp chỗ trống bằng ngữ cảnh có sẵn
    // trong prompt (tên dự án, mô tả dự án) và cho ra tóm tắt không có chữ nào từ file.
    public static class ContentSignalPolicy
    {
        // File bản vẽ scan / ảnh không rơi vào đây mà rơi vào nhánh "trích ra rỗng" phía trên,
        // nên ngưỡng này chỉ bắt đúng loại: CÓ chữ nhưng gần như không có nội dung.
        public const int MinChars = 200;
        public const int MinWords = 30;

        public static bool HasEnoughSignal(string? text)
        {
            var t = text?.Trim();
            if (string.IsNullOrEmpty(t) || t.Length < MinChars) return false;
            return CountWords(t) >= MinWords;
        }

        // Đếm theo cụm chữ/số liền nhau: text trích từ PDF hay dính chữ hoặc vỡ dòng,
        // tách theo khoảng trắng sẽ đếm sai cả hai chiều.
        private static int CountWords(string t)
        {
            int words = 0;
            bool inWord = false;
            foreach (var ch in t)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (!inWord) { words++; inWord = true; }
                }
                else inWord = false;
            }
            return words;
        }
    }
}
