using Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class TextChunkerService : ITextChunker
    {
        private readonly IConfiguration _configuration;

        public TextChunkerService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IReadOnlyList<string> Split(string text)
        {
            var chunkSize = _configuration.GetValue<int>("TextChunker:ChunkSize");
            var overlapSize = _configuration.GetValue<int>("TextChunker:ChunkOverlap");

            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            if (chunkSize <= 0) chunkSize = 2000;
            if (overlapSize < 0) overlapSize = 0; 
            if (overlapSize >= chunkSize) overlapSize = chunkSize / 2; 

            var chunks = new List<string>();
            int step = chunkSize - overlapSize;
            int start = 0;
            while (start < text.Length)
            {
                int len = Math.Min(chunkSize, text.Length - start);
                var piece = text.Substring(start, len).Trim();
                if(piece.Length > 0)
                    chunks.Add(piece);
                start += step;
            }
            return chunks;
        }
    }
}
