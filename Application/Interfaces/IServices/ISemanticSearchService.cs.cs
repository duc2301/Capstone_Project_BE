using Application.DTOs.ResponseDTOs.Search;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface ISemanticSearchService
    {
        Task<IReadOnlyList<FileSearchResultDTO>> SearchAsync(
            Guid projectId, string query, CancellationToken ct = default);
    }
}
