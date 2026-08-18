using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Rag;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class DocumentIndexRepository : IDocumentIndexRepository
    {
        private readonly CDESystemDbContext _context;

        public DocumentIndexRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Guid>> GetUnindexedFileItemIdsAsync(CancellationToken ct = default)
        {
            var documents = _context.Set<Document>().AsNoTracking();

            // Document.SourceFileVersionId chính là FileVersionState.Id mà FileItem.CurrentVersionId trỏ tới
            // (xem IFileContentReader.ExtractedFile) -> so hai cột này là biết phiên bản HIỆN HÀNH đã embed chưa,
            // không cần đụng tới ContentHash.
            // Điều kiện vùng viết tay thay vì gọi CdeAreaExtensions.IsOfficialArea() vì EF không dịch được
            // extension method sang SQL — đổi luật vùng thì sửa cả hai chỗ (chỗ kia là nguồn chuẩn).
            return await _context.Set<FileItem>().AsNoTracking()
                .Where(f => f.CurrentVersionId != null
                         && (f.Folder.Area == CdeArea.Published || f.Folder.Area == CdeArea.Archived)
                         && !documents.Any(d => d.FileItemId == f.Id
                                             && d.Status == DocumentIngestStatus.Embedded
                                             && d.SourceFileVersionId == f.CurrentVersionId))
                .Select(f => f.Id)
                .ToListAsync(ct);
        }
    }
}
