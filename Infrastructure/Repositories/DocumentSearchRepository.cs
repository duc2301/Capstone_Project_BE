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

        public async Task<IReadOnlyList<DocumentSearchHit>> SearchByVectorAsync(
            Guid projectId, Vector queryEmbedding, int k,
            IReadOnlyCollection<Guid>? viewableFolderIds,
            IReadOnlyCollection<Guid>? extraViewableFileIds = null,
            CancellationToken ct = default)
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

            // Lọc theo quyền xem thư mục. Chỉ áp lên thư mục CHỨA KẾT QUẢ — cố ý KHÔNG áp lên truy vấn
            // con `src.Folder.Area == Published` ở trên: đó là kiểm tra trạng thái (bản Published còn tồn
            // tại hay không), không phải kiểm tra đọc được hay không. Lẫn hai thứ này sẽ làm cờ
            // IsUnderRevision báo sai cho người không nhìn thấy thư mục Published của nhóm khác.
            //
            // Vùng ứng viên = thư mục xem được HOẶC tệp được cấp quyền riêng lẻ. Thiếu vế thứ hai thì
            // "phân quyền cho đúng một tệp" chạy đúng ở mọi màn hình trừ tìm kiếm, vì quyền cấp theo
            // tệp không nằm trong tập thư mục. Đây chỉ là bộ lọc THÔ để giữ recall khi cắt top-k ở DB;
            // quyết định cuối cùng vẫn do SemanticSearchService hỏi lại từng tệp.
            if (viewableFolderIds is not null)
            {
                var extraFileIds = extraViewableFileIds ?? Array.Empty<Guid>();
                q = extraFileIds.Count == 0
                    ? q.Where(x => viewableFolderIds.Contains(x.f.FolderId))
                    : q.Where(x => viewableFolderIds.Contains(x.f.FolderId)
                                || extraFileIds.Contains(x.f.Id));
            }

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
