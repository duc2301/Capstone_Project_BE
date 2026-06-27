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
            int maxParentChars = int.Parse(_configuration["MaxParentChars"]);
            int maxChildChars = int.Parse(_configuration["MaxChildChars"]);
            int childOverlap = int.Parse(_configuration["ChildOverlap"]);

            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<ChunkedParent>();
            if (childOverlap >= maxChildChars) childOverlap = maxChildChars / 4;   // guard

            var paragraphs = SplitParagraphs(text);
            var parents = GroupIntoParents(paragraphs, maxParentChars);

            return parents
                .Select(p => new ChunkedParent(p, SplitChildren(p, maxChildChars, childOverlap)))
                .Where(cp => cp.Children.Count > 0)
                .ToList();
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

        private static List<string> SplitChildren(string parent, int maxChildChars, int overlap)
        {
            var children = new List<string>();
            int start = 0;

            while (start < parent.Length)
            {
                int end = Math.Min(start + maxChildChars, parent.Length);

                // Nếu chưa hết text, lùi 'end' về dấu ngắt gần nhất để không cắt giữa từ/câu.
                if (end < parent.Length)
                {
                    int cut = parent.LastIndexOfAny(Breakers, end - 1, end - start);
                    if (cut > start + maxChildChars / 2)   // chỉ lùi nếu không quá ngắn
                        end = cut + 1;
                }

                var child = parent[start..end].Trim();
                if (child.Length > 0) children.Add(child);

                if (end >= parent.Length) break;
                start = Math.Max(end - overlap, start + 1);   // overlap + đảm bảo luôn tiến (chống treo)
            }
            return children;
        }
    }
}
