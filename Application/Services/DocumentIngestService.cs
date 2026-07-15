using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Rag;
using Pgvector;

namespace Application.Services
{
    public class DocumentIngestService : IDocumentIngestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileContentReader _reader;
        private readonly ITextChunker _chunker;
        private readonly IEmbeddingService _embedding;
        private readonly IChunkContextEnricher _enricher;

        public DocumentIngestService(IUnitOfWork unitOfWork, IFileContentReader reader, ITextChunker chunker, IEmbeddingService embedding, IChunkContextEnricher enricher)
        {
            _unitOfWork = unitOfWork;
            _reader = reader;
            _chunker = chunker;
            _embedding = embedding;
            _enricher = enricher;
        }

        public async Task<Guid> IngestFileAsync(Guid fileItemId, CancellationToken ct = default)
        {
            // Đọc file dùng chung: load FileItem + version + trích text + sanitize
            var extracted = await _reader.LoadTextAsync(fileItemId, ct);
            if (extracted is null)
                throw new ApiExceptionResponse("Không tìm thấy file hoặc phiên bản nội dung", 404);

            var fileItem = extracted.Item;
            var version = extracted.Version;
            var text = extracted.Text;

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);
            if (folder == null)
                throw new ApiExceptionResponse("Folder not found", 404);

            if (folder.Area != CdeArea.Published)
            {
                throw new ApiExceptionResponse("Chỉ đọc file khi đã ở published", 400);
            }

            var contentHash = version.Checksum;
            var existing = await _unitOfWork.Repository<Document>()
                .FindAsync(d => d.FileItemId == fileItemId);
            var alreadyEmbedded = existing.FirstOrDefault(
                d => d.Status == DocumentIngestStatus.Embedded && d.ContentHash == contentHash);
            if (alreadyEmbedded is not null)
                return alreadyEmbedded.Id;

            if (string.IsNullOrWhiteSpace(text))
                throw new ApiExceptionResponse("Không trích được nội dung văn bản", 400);

            var parents = _chunker.Chunk(text);
            if (parents.Count == 0) 
                throw new ApiExceptionResponse("Tài liệu không có nội dung để chia chunk", 400);

            var document = new Document
            {
                Id = Guid.NewGuid(),
                SourceFileVersionId = version.Id,
                FileItemId = fileItem.Id,
                ProjectId = folder.ProjectId,
                Area = folder.Area,
                FileName = fileItem.Name,
                Format = version.Format,
                Revision = version.VersionNumber.ToString(),
                ContentHash = contentHash,
                Discipline = null,            // để sau, chưa có nguồn
                Status = DocumentIngestStatus.Pending
            };

            var prefix = $"[Tài liệu: {fileItem.Name} | Dự án: {folder.ProjectId} | Khu vực: {folder.Area} | Bản: {document.Revision}]";

            var parentEntities = new List<DocumentParentChunk>(parents.Count);
            for (int p = 0; p < parents.Count; p++)
                parentEntities.Add(new DocumentParentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    ProjectId = folder.ProjectId,
                    ChunkIndex = p,
                    Content = parents[p].Content
                });

            var ctxs = new string?[parents.Count];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, parents.Count),
                new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                async (p, token) => ctxs[p] = await _enricher.EnrichAsync(prefix, parents[p].Content, token));

            int childIndex = 0;
            var toEmbed = new List<string>();
            var childEntities = new List<DocumentChildChunk>();
            for (int p = 0; p < parents.Count; p++)
            {
                var ctx = ctxs[p];
                if (!string.IsNullOrWhiteSpace(ctx))
                    parentEntities[p].Content = ctx + "\n\n" + parents[p].Content;

                foreach (var child in parents[p].Children)
                {
                    var embedInput = string.IsNullOrWhiteSpace(ctx) ? $"{prefix}\n\n{child}" : $"{prefix}\n{ctx}\n\n{child}";
                    var ce = new DocumentChildChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        ProjectId = folder.ProjectId,
                        ParentChunkId = parentEntities[p].Id,
                        ChunkIndex = childIndex++,
                        Content = child,
                        CreatedAt = DateTime.UtcNow
                    };
                    parentEntities[p].ChildChunks.Add(ce);
                    toEmbed.Add(embedInput);
                    childEntities.Add(ce);
                }
                document.Chunks.Add(parentEntities[p]);
            }

            var vectors = await _embedding.EmbedBatchAsync(toEmbed, ct);
            for (int i = 0; i < childEntities.Count; i++)
                childEntities[i].Embedding = new Vector(vectors[i]);

            foreach (var old in existing)
                _unitOfWork.Repository<Document>().Delete(old);
            document.Status = DocumentIngestStatus.Embedded;
            document.IngestedAt = DateTime.UtcNow;
            document.ChunkCount = childIndex;
            await _unitOfWork.Repository<Document>().CreateAsync(document);
            await _unitOfWork.SaveChangesAsync();
            return document.Id;        
        }
    }
}
