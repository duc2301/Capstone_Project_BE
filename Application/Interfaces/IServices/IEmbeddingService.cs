using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IEmbeddingService
    {
        Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
        Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    }
}
