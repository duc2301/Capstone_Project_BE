using Domain.Entities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface ITextChunker
    {
        public record ChunkedParent(string Content, IReadOnlyList<string> Children);
        IReadOnlyList<ChunkedParent> Chunk(string text);
    }
}
