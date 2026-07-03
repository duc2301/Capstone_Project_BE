using Application.DTOs.ResponseDTOs.Search;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Pgvector;

namespace Application.Services
{
    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IDocumentSearchRepository _searchSematicRepo;

        public SemanticSearchService(IEmbeddingService embeddingService, IDocumentSearchRepository searchSematicRepo)
        {
            _embeddingService = embeddingService;
            _searchSematicRepo = searchSematicRepo;
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

            return hits
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
        }

        // qwen3-embedding: câu QUERY cần "instruct", còn DOCUMENT thì KHÔNG (bất đối xứng có chủ đích)
        private static string BuildQuery(string query) =>
            "Instruct: Given a search query, retrieve relevant document passages.\n" +
            $"Query: {query}";
    }
}
