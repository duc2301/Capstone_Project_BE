namespace Application.Services.Loi
{
    public static class LoiParamSuggester
    {
        public const double MinScore = 0.5;

        private static readonly HashSet<string> NoiseTokens = new(StringComparer.Ordinal)
        {
            "thiet", "ke", "cua", "theo", "va", "cau", "kien", "so"
        };

        public sealed record Suggestion(string ParamNameNormalized, double Score);

        public static Suggestion? Suggest(string unknownNormalized, IEnumerable<string> candidatesNormalized)
        {
            var unknownTokens = Tokenize(unknownNormalized);
            if (unknownTokens.Count == 0) return null;

            string? bestName = null;
            double bestScore = 0;

            foreach (var candidate in candidatesNormalized)
            {
                var score = Score(unknownTokens, Tokenize(candidate));
                if (score < MinScore) continue;

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

        private static double Score(List<string> left, List<string> right)
        {
            var a = Meaningful(left);
            var b = Meaningful(right);
            if (a.Count == 0 || b.Count == 0) return 0;

            var shared = a.Count(a1 => b.Contains(a1));
            if (shared == 0) return 0;

            var score = shared / (double)Math.Min(a.Count, b.Count);
            return shared == 1 && Math.Max(a.Count, b.Count) > 2 ? score * 0.75 : score;
        }

        private static HashSet<string> Meaningful(List<string> tokens)
        {
            var kept = new HashSet<string>(tokens.Where(t => !NoiseTokens.Contains(t)), StringComparer.Ordinal);
            return kept.Count > 0 ? kept : new HashSet<string>(tokens, StringComparer.Ordinal);
        }
    }
}
