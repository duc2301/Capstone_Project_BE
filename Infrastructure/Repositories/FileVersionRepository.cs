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

        // Không AsNoTracking: service cần entity tracked để set IsCurrent = false rồi SaveChangesAsync.
        public async Task<FileVersionState?> GetCurrentStateAsync(Guid fileItemId)
        {
            return await _context.FileVersionStates
                .FirstOrDefaultAsync(s => s.FileItemId == fileItemId && s.IsCurrent);
        }

        public async Task<List<FileVersionState>> GetHistoryAsync(Guid fileItemId)
        {
            return await _context.FileVersionStates
                .Where(s => s.FileItemId == fileItemId)
                .OrderByDescending(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

    }
}
