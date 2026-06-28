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
        private readonly IFileStorageService _storage;
        private readonly IFileTextExtractor _extractor;
        private readonly ITextChunker _chunker;
        private readonly IEmbeddingService _embedding;

        public DocumentIngestService(IUnitOfWork unitOfWork, IFileStorageService storage, IFileTextExtractor extractor, ITextChunker chunker, IEmbeddingService embedding)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _extractor = extractor;
            _chunker = chunker;
            _embedding = embedding;
        }

        public async Task<Guid> IngestFileAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (fileItem == null)
            {
                throw new ApiExceptionResponse("File item not found", 404);
            } else if (fileItem.CurrentVersionId is null)
            {
                throw new ApiExceptionResponse("File chưa có phiên bản nội dung", 400);
            }

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId);
            if (version == null)
                throw new ApiExceptionResponse("File version not found", 404);

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

            await using var stream = await _storage.OpenReadAsync(version.StoragePath, ct);
            var text = await _extractor.ExtractTextAsync(stream, version.Format, ct);

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
            int childIndex = 0;
            for (int p = 0; p < parents.Count; p++)
            {
                var parentEntity = new DocumentParentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    ProjectId = folder.ProjectId,
                    ChunkIndex = p,
                    Content = parents[p].Content
                };
                foreach (var child in parents[p].Children)
                {
                    var vector = await _embedding.EmbedAsync($"{prefix}\n\n{child}", ct);
                    parentEntity.ChildChunks.Add(new DocumentChildChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        ProjectId = folder.ProjectId,
                        ParentChunkId = parentEntity.Id,
                        ChunkIndex = childIndex++,
                        Content = child,
                        Embedding = new Vector(vector),
                        CreatedAt = DateTime.UtcNow
                    });
                }                                 
                document.Chunks.Add(parentEntity); 
            }                                      

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
