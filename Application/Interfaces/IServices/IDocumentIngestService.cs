using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IDocumentIngestService
    {
        Task<Guid> IngestFileAsync(Guid fileItemId, CancellationToken ct = default);
    }
}
