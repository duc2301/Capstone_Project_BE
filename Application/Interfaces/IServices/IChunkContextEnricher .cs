using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IChunkContextEnricher
    {
        Task<string?> EnrichAsync(string fileMeta, string parentContent, CancellationToken ct = default);
    }
}
