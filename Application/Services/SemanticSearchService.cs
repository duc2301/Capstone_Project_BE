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
        private readonly IPermissionCheckingService _permission;

        public SemanticSearchService(IEmbeddingService embeddingService, IDocumentSearchRepository searchSematicRepo, IUnitOfWork unitOfWork, IPermissionCheckingService permission)
        {
            _embeddingService = embeddingService;
            _searchSematicRepo = searchSematicRepo;
            _unitOfWork = unitOfWork;
            _permission = permission;
        }

        private const int CandidateK = 40;      
        private const int MaxFiles = 10;        
        private const double MaxDistance = 0.4;

        public async Task<IReadOnlyList<FileSearchResultDTO>> SearchAsync(
            Guid projectId, string query, Guid actorId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ApiExceptionResponse("Câu tìm kiếm rỗng", 400);

            // Admin hệ thống / PM / ProjectAdmin tra cứu toàn dự án; còn lại chỉ trong thư mục được cấp quyền xem.
            // Cùng cặp đôi đang dùng ở AuditLogService.GetMyInProjectAsync — mặc định từ chối.
            IReadOnlyCollection<Guid>? viewableFolderIds = null;
            var hasFullAccess = await _permission.HasProjectFullAccessAsync(projectId, actorId);
            if (!hasFullAccess)
            {
                var viewable = await _permission.GetViewableFolderIdsAsync(projectId, actorId);

                // Không được xem thư mục nào -> không có gì để trả, và khỏi tốn một lượt embed qua Ollama.
                if (viewable.Count == 0)
                    return Array.Empty<FileSearchResultDTO>();

                viewableFolderIds = viewable;
            }

            var qVec = new Vector(await _embeddingService.EmbedAsync(BuildQuery(query), ct));
            var hits = await _searchSematicRepo.SearchByVectorAsync(projectId, qVec, CandidateK, viewableFolderIds, ct);

            // Gom theo file và xếp hạng theo đoạn khớp nhất, nhưng CHƯA cắt còn MaxFiles: bước lọc
            // quyền cấp file bên dưới có thể loại bớt, cắt trước sẽ làm hụt kết quả trả về.
            var grouped = hits
                .Where(h => h.Distance <= MaxDistance)
                .GroupBy(h => h.FileItemId)
                .OrderBy(g => g.Min(h => h.Distance))
                .ToList();

            // Quyền cấp FILE. Bộ lọc thư mục ở trên chưa đủ: một dòng FilePermission (override nhóm
            // hoặc override riêng theo tài khoản) có thể chặn đúng file này dù thư mục vẫn xem được.
            // Thiếu bước này thì tìm kiếm trả về nguyên đoạn nội dung của tài liệu người dùng bị cấm
            // xem, trong khi danh sách file của thư mục đã ẩn nó đi
            // (FolderTreeService.GetFilesByFolderAsync -> GetDeniedViewFileIdsInFolderAsync).
            // Quyết định qua GetViewableFileIdsAmongAsync để thứ tự ưu tiên (grant thắng deny,
            // override cá nhân thắng ACL nhóm) chỉ có đúng một nơi định nghĩa.
            if (!hasFullAccess && grouped.Count > 0)
            {
                var viewableFileIds = await _permission.GetViewableFileIdsAmongAsync(
                    actorId, grouped.Select(g => g.Key).ToList());
                grouped = grouped.Where(g => viewableFileIds.Contains(g.Key)).ToList();
            }

            var results = grouped
                .Take(MaxFiles)
                .Select(g =>
                {
                    var best = g.OrderBy(h => h.Distance).First(); // đoạn khớp nhất của file
                    return new FileSearchResultDTO
                    {
                        FileItemId = best.FileItemId,
                        FileName = best.FileName,
                        Snippet = best.Snippet,
                        Similarity = Math.Round(1 - best.Distance, 3),
                        MatchCount = g.Count(),
                        Area = best.Area.ToString(),
                        IsUnderRevision = best.IsUnderRevision
                    };
                })
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
