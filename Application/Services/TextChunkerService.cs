using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using static Application.Interfaces.IServices.ITextChunker;

namespace Application.Services
{
    public class TextChunkerService : ITextChunker
    {
        private readonly IConfiguration _configuration;

        public TextChunkerService(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        private static readonly char[] Breakers = { '.', '!', '?', '\n', ';', ' ' };

        public IReadOnlyList<ChunkedParent> Chunk(string text)
        {
            int maxParentChars = int.Parse(_configuration["TextChunker:ParentChunkSize"]);
            int maxChildChars = int.Parse(_configuration["TextChunker:ChildChunkSize"]);
            int childOverlap = int.Parse(_configuration["TextChunker:ChildOverlap"]);

            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<ChunkedParent>();
            if (childOverlap >= maxChildChars) childOverlap = maxChildChars / 4;   // guard

            var paragraphs = SplitParagraphs(text);
            var parents = GroupIntoParents(paragraphs, maxParentChars);

            return parents
                .Select(p => new ChunkedParent(p, SplitChildren(p, maxChildChars, childOverlap)))
                .Where(cp => cp.Children.Count > 0)
                .ToList();
        }


        public IReadOnlyList<string> ChunkForExtraction(string text)
        {
            int maxSliceChars = ReadInt("TextChunker:ExtractionSliceSize", 28000);
            int overlapChars = ReadInt("TextChunker:ExtractionOverlap", 800);

            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            if (overlapChars >= maxSliceChars) overlapChars = maxSliceChars / 10;   // guard

            // Đoạn đơn lẻ dài hơn trần (bảng lớn không có dòng trống) phải cắt cứng trước,
            // nếu không GroupIntoParents sẽ nhả ra một lát vượt context.
            var paragraphs = SplitParagraphs(text)
                .SelectMany(p => p.Length <= maxSliceChars
                    ? (IEnumerable<string>)new[] { p }
                    : HardSplit(p, maxSliceChars))
                .ToList();

            // Gộp tới ngưỡng đã trừ overlap, vì AddOverlap còn chèn thêm phần đuôi lát trước
            // vào đầu mỗi lát — gộp thẳng tới maxSliceChars sẽ làm lát cuối cùng vượt trần.
            return AddOverlap(GroupIntoParents(paragraphs, maxSliceChars - overlapChars), overlapChars);
        }

        private int ReadInt(string key, int fallback)
            => int.TryParse(_configuration[key], out var v) && v > 0 ? v : fallback;

        // Cắt một đoạn quá dài tại ranh giới dòng gần nhất, không cắt giữa từ.
        private static List<string> HardSplit(string paragraph, int maxChars)
        {
            var parts = new List<string>();
            int pos = 0;
            while (pos < paragraph.Length)
            {
                int len = Math.Min(maxChars, paragraph.Length - pos);
                if (pos + len < paragraph.Length)
                {
                    int nl = paragraph.LastIndexOf('\n', pos + len - 1, len);
                    if (nl > pos) len = nl - pos + 1;
                }
                var part = paragraph.Substring(pos, len).Trim();
                if (part.Length > 0) parts.Add(part);
                pos += len;
            }
            return parts;
        }

        // Lặp phần đuôi của lát trước vào đầu lát sau: bảng/danh sách bị cắt ngang vẫn còn ngữ cảnh.
        // Phần lặp này do bước gộp kết quả ở tầng trên khử trùng.
        private static List<string> AddOverlap(List<string> slices, int overlapChars)
        {
            if (overlapChars <= 0 || slices.Count <= 1) return slices;

            var result = new List<string>(slices.Count) { slices[0] };
            for (int i = 1; i < slices.Count; i++)
            {
                var prev = slices[i - 1];
                var tail = prev[^Math.Min(overlapChars, prev.Length)..];

                int nl = tail.IndexOf('\n');                 // bắt đầu overlap từ đầu dòng cho sạch
                if (nl >= 0 && nl < tail.Length - 1) tail = tail[(nl + 1)..];

                result.Add(tail + "\n\n" + slices[i]);
            }
            return result;
        }

        // Tách văn bản thành các "đoạn" theo dòng trống (\n\n) — ranh giới tự nhiên nhất.
        private static List<string> SplitParagraphs(string text)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");   // chuẩn hoá xuống dòng
            return text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                       .Select(p => p.Trim())
                       .Where(p => p.Length > 0)
                       .ToList();
        }

        private static List<string> GroupIntoParents(List<string> paragraphs, int maxParentChars)
        {
            var parents = new List<string>();
            var buf = new StringBuilder();

            foreach (var p in paragraphs)
            {
                // Nếu thêm đoạn này mà vượt ngưỡng -> chốt parent hiện tại, mở parent mới.
                if (buf.Length > 0 && buf.Length + p.Length + 2 > maxParentChars)
                {
                    parents.Add(buf.ToString().Trim());
                    buf.Clear();
                }
                if (buf.Length > 0) buf.Append("\n\n");
                buf.Append(p);
            }
            if (buf.Length > 0) parents.Add(buf.ToString().Trim());
            return parents;
        }

        private static List<string> SplitChildren(string parent, int maxChildChars, int overlapChars)
        {
            var sentences = SplitSentences(parent);
            var children = new List<string>();
            var current = new List<string>();
            int currentLen = 0;

            foreach (var s in sentences)
            {
                // vượt ngưỡng -> chốt child, mở child mới với overlap là vài câu cuối
                if (currentLen > 0 && currentLen + s.Length + 1 > maxChildChars)
                {
                    children.Add(string.Join(" ", current));

                    var carry = new List<string>();
                    int carryLen = 0;
                    for (int i = current.Count - 1; i >= 0 && carryLen < overlapChars; i--)
                    {
                        carry.Insert(0, current[i]);
                        carryLen += current[i].Length + 1;
                    }
                    current = carry;
                    currentLen = carryLen;
                }
                current.Add(s);
                currentLen += s.Length + 1;
            }
            if (current.Count > 0) children.Add(string.Join(" ", current));
            return children;
        }

        private static List<string> SplitSentences(string text)
        {
            var result = new List<string>();
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool punctEnd = (c == '.' || c == '!' || c == '?' || c == '…')
                                && (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]));
                if (punctEnd || c == '\n')
                {
                    var s = text[start..(i + 1)].Trim();
                    if (s.Length > 0) result.Add(s);
                    start = i + 1;
                }
            }
            if (start < text.Length)
            {
                var s = text[start..].Trim();
                if (s.Length > 0) result.Add(s);
            }
            return result;
        }
    }
}
