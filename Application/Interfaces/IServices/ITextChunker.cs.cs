using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface ITextChunker
    {
        IReadOnlyList<string> Split(string text, int maxChars, int overlap);
    }
}
