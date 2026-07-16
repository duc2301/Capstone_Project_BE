using Application.DTOs.ResponseDTOs.Search;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Pgvector;

namespace Application.Services
{
    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IDocumentSearchRepository _searchSematicRepo;
        private readonly IUnitOfWork _unitOfWork;

        public SemanticSearchService(IEmbeddingService embeddingService, IDocumentSearchRepository searchSematicRepo, IUnitOfWork unitOfWork)
        {
            _embeddingService = embeddingService;
            _searchSematicRepo = searchSematicRepo;
            _unitOfWork = unitOfWork;
        }

        private const int CandidateK = 40;      
        private const int MaxFiles = 10;        
        private const double MaxDistance = 0.4;

        public async Task<IReadOnlyList<FileSearchResultDTO>> SearchAsync(
            Guid projectId, string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ApiExceptionResponse("Câu tìm kiếm rỗng", 400);

            var qVec = new Vector(await _embeddingService.EmbedAsync(BuildQuery(query), ct));
            var hits = await _searchSematicRepo.SearchByVectorAsync(projectId, qVec, CandidateK, ct);

            var results = hits
                .Where(h => h.Distance <= MaxDistance)
                .GroupBy(h => h.FileItemId)
                .Select(g =>
                {
                    var best = g.OrderBy(h => h.Distance).First(); // đoạn khớp nhất của file
                    return new FileSearchResultDTO
                    {
                        FileItemId = best.FileItemId,
                        FileName = best.FileName,
                        Snippet = best.Snippet,
                        Similarity = Math.Round(1 - best.Distance, 3),
                        MatchCount = g.Count()
                    };
                })
                .OrderByDescending(r => r.Similarity)
                .Take(MaxFiles)
                .ToList();

            if (results.Count == 0) return results;

            // Document không giữ FolderId -> lấy từ FileItem để FE mở đúng trang xem chi tiết.
            var fileIds = results.Select(r => r.FileItemId).ToList();
            var folderByFileId = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => fileIds.Contains(f.Id)))
                .ToDictionary(f => f.Id, f => f.FolderId);

            foreach (var r in results)
                if (folderByFileId.TryGetValue(r.FileItemId, out var folderId))
                    r.FolderId = folderId;

            return results;
        }

        // qwen3-embedding: câu QUERY cần "instruct", còn DOCUMENT thì KHÔNG (bất đối xứng có chủ đích)
        private static string BuildQuery(string query) =>
            "Instruct: Given a search query, retrieve relevant document passages.\n" +
            $"Query: {query}";
    }
}
