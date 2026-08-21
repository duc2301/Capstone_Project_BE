using Application.Interfaces.IRepositories;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FileViewRepository : IFileViewRepository
    {
        private readonly CDESystemDbContext _context;

        public FileViewRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileItemId, ct);
        }

        public async Task<FileVersionState?> GetVersionForUpdateAsync(
            Guid versionId, CancellationToken ct = default)
        {
            return await _context.FileVersionStates
                .FirstOrDefaultAsync(v => v.Id == versionId, ct);
        }

        public async Task<Folder?> GetFolderAsync(Guid folderId, CancellationToken ct = default)
        {
            return await _context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == folderId, ct);
        }
    }
}
