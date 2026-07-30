namespace Application.Services.Loi
{
    // Đoán tham số chuẩn cho một tên lạ trong file IFC, để người dùng xác nhận thành alias của dự án.
    // Hàm thuần: không DB, không I/O.
    public static class LoiParamSuggester
    {
        // Dưới ngưỡng này thì im lặng, còn hơn gợi ý bừa.
        public const double MinScore = 0.5;

        // Từ quá phổ biến, khớp được cũng không nói lên gì.
        private static readonly HashSet<string> NoiseTokens = new(StringComparer.Ordinal)
        {
            "thiet", "ke", "cua", "theo", "va", "cau", "kien", "so"
        };

        public sealed record Suggestion(string ParamNameNormalized, double Score);

        public static Suggestion? Suggest(string unknownNormalized, IEnumerable<string> candidatesNormalized)
        {
            var unknownTokens = Tokenize(unknownNormalized);
            if (unknownTokens.Count == 0) return null;

            // So sánh trên điểm THÔ, chỉ làm tròn lúc trả về — làm tròn sớm thì nhánh gỡ hoà không chạy.
            string? bestName = null;
            double bestScore = 0;

            foreach (var candidate in candidatesNormalized)
            {
                var score = Score(unknownTokens, Tokenize(candidate));
                if (score < MinScore) continue;

                // Bằng điểm thì chốt theo thứ tự chữ cái để gợi ý tất định, không phụ thuộc thứ tự duyệt.
                if (bestName is null
                    || score > bestScore
                    || (score == bestScore && string.CompareOrdinal(candidate, bestName) < 0))
                {
                    bestName = candidate;
                    bestScore = score;
                }
            }

            return bestName is null ? null : new Suggestion(bestName, Math.Round(bestScore, 2));
        }

        private static List<string> Tokenize(string normalized) =>
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Hệ số bao hàm |giao| / |tập nhỏ hơn|, không dùng Jaccard vì hậu tố rác "thiết kế"
        // làm phình mẫu số ("chiều rộng thiết kế" vs "chiều rộng" phải khớp hoàn toàn).
        private static double Score(List<string> left, List<string> right)
        {
            var a = Meaningful(left);
            var b = Meaningful(right);
            if (a.Count == 0 || b.Count == 0) return 0;

            var shared = a.Count(a1 => b.Contains(a1));
            if (shared == 0) return 0;

            var score = shared / (double)Math.Min(a.Count, b.Count);
            // Chung đúng một từ dễ trùng ngẫu nhiên ("tích" trong "thể tích" và "diện tích") -> hạ điểm.
            return shared == 1 && Math.Max(a.Count, b.Count) > 2 ? score * 0.75 : score;
        }

        private static HashSet<string> Meaningful(List<string> tokens)
        {
            var kept = new HashSet<string>(tokens.Where(t => !NoiseTokens.Contains(t)), StringComparer.Ordinal);
            // Toàn từ rác thì dùng nguyên bản, còn hơn không so được gì.
            return kept.Count > 0 ? kept : new HashSet<string>(tokens, StringComparer.Ordinal);
        }
    }
}
