using Application.Interfaces.IRepositories;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FileVersionRepository : GenericRepository<FileVersionState>, IFileVersionRepository
    {
        private readonly CDESystemDbContext _context;

        public FileVersionRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        // Tài liệu hiện có = FileItem cùng Name trong cùng folder (theo yêu cầu giai đoạn này).
        public async Task<FileItem?> FindExistingDocumentAsync(Guid folderId, string fileName)
        {
            return await _context.FileItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FolderId == folderId && f.Name == fileName);
        }

        // Không AsNoTracking: service cần entity tracked để cập nhật rồi SaveChangesAsync.
        public async Task<FileVersionState?> GetVersionStateAsync(Guid fileItemId)
        {
            return await _context.FileVersionStates
                .FirstOrDefaultAsync(s => s.FileItemId == fileItemId);
        }

        public async Task<int> CountFileVersionsAsync(Guid fileItemId)
        {
            return await _context.FileVersions
                .CountAsync(v => v.FileItemId == fileItemId);
        }
    }
}
