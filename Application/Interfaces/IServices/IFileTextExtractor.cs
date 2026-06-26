using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IFileTextExtractor
    {
        bool CanExtract(string format);
        Task<string> ExtractTextAsync(Stream Content, string format, CancellationToken cancellationToken = default);
    }
}
