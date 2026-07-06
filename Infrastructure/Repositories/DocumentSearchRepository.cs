using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Rag;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using static Application.Interfaces.IRepositories.IDocumentSearchRepository;

namespace Infrastructure.Repositories
{
    public class DocumentSearchRepository : IDocumentSearchRepository
    {
        private readonly CDESystemDbContext _context;

        public DocumentSearchRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<DocumentSearchHit>> SearchByVectorAsync(Guid projectId, Vector queryEmbedding, int k, CancellationToken ct = default)
        {
            return await _context.Set<DocumentChildChunk>()
                .AsNoTracking()
                .Include(c => c.ParentChunk).ThenInclude(p => p.Document)
                .Where(c => c.ProjectId == projectId
                && c.ParentChunk.Document.Status == DocumentIngestStatus.Embedded)
                //&& c.ParentChunk.Document.Area == CdeArea.Published)
                .OrderBy(c => c.Embedding!.CosineDistance(queryEmbedding))
                .Take(k)
                .Select(c => new DocumentSearchHit(
                c.ParentChunk.Document.FileItemId,
                c.ParentChunk.Document.FileName,
                c.Content,
                c.ParentChunk.Content,
                c.Embedding!.CosineDistance(queryEmbedding)))
                .ToListAsync(ct);
        }
    }
}
