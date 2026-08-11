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
            var files = _context.Set<FileItem>().AsNoTracking();
            var q = from c in _context.Set<DocumentChildChunk>().AsNoTracking()
                    join f in files on c.ParentChunk.Document.FileItemId equals f.Id
                    where c.ProjectId == projectId
                       && c.ParentChunk.Document.Status == DocumentIngestStatus.Embedded
                       && (f.Folder.Area == CdeArea.Published
                           || (f.Folder.Area == CdeArea.Archived
                               && !files.Any(src => src.Id == f.SourceFileItemId
                                                 && src.Folder.Area == CdeArea.Published)))
                    select new { c, f };

            return await q
                .OrderBy(x => x.c.Embedding!.CosineDistance(queryEmbedding))
                .Take(k)
                .Select(x => new DocumentSearchHit(
                x.f.Id,
                x.c.ParentChunk.Document.FileName,
                x.c.Content,
                x.c.ParentChunk.Content,
                x.c.Embedding!.CosineDistance(queryEmbedding),
                x.f.Folder.Area,
                x.f.Folder.Area == CdeArea.Archived
                    && files.Any(src => src.Id == x.f.SourceFileItemId)))
                .ToListAsync(ct);
        }
    }
}
