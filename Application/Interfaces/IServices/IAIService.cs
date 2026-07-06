using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IAIService
    {
        Task<FileNameCheckResult> CheckNameMatchesContentAsync(Guid fileItemId, CancellationToken ct = default);

        record FileNameCheckResult(bool Matches, double Confidence, string? Reason);
    }
}
