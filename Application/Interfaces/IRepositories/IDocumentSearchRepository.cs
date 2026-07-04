using Domain.Entities;
using Pgvector;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IRepositories
{
    public interface IDocumentSearchRepository
    {
        Task<IReadOnlyList<DocumentSearchHit>> SearchByVectorAsync(
           Guid projectId, Vector queryEmbedding, int k, CancellationToken ct = default);

        public record DocumentSearchHit(
        Guid FileItemId,
        string FileName,
        string Snippet,          // nội dung child khớp
        string? ParentContext,
        double Distance);
    }
    
}
