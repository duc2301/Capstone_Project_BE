using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Cde;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FileLinkRepository : IFileLinkRepository
    {
        private readonly CDESystemDbContext _context;

        public FileLinkRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileItemId, ct);
        }

        public async Task<Folder?> GetFolderAsync(Guid folderId, CancellationToken ct = default)
        {
            return await _context.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == folderId, ct);
        }

        public async Task<IReadOnlyList<FileLink>> GetLinksOfFileAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileLinks
                .AsNoTracking()
                .Where(l => l.FileItemId == fileItemId || l.LinkedFileItemId == fileItemId)
                .ToListAsync(ct);
        }

        public async Task<FileLink?> FindLinkPairForUpdateAsync(
            Guid first, Guid second, CancellationToken ct = default)
        {
            return await _context.FileLinks
                .FirstOrDefaultAsync(l => l.FileItemId == first && l.LinkedFileItemId == second, ct);
        }

        public async Task<IReadOnlyList<FileItem>> GetFileItemsByIdsAsync(
            IEnumerable<Guid> fileItemIds, CancellationToken ct = default)
        {
            var ids = fileItemIds.Distinct().ToList();
            if (ids.Count == 0) return Array.Empty<FileItem>();

            return await _context.FileItems
                .AsNoTracking()
                .Where(f => ids.Contains(f.Id))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<FileItem>> GetFileItemsInFoldersAsync(
            IEnumerable<Guid> folderIds, Guid? excludeFileItemId, CancellationToken ct = default)
        {
            var ids = folderIds.Distinct().ToList();
            if (ids.Count == 0) return Array.Empty<FileItem>();

            var query = _context.FileItems
                .AsNoTracking()
                .Where(f => ids.Contains(f.FolderId));

            if (excludeFileItemId.HasValue)
                query = query.Where(f => f.Id != excludeFileItemId.Value);

            return await query.ToListAsync(ct);
        }

        public async Task<IReadOnlyDictionary<Guid, Folder>> GetFoldersByIdsAsync(
            IEnumerable<Guid> folderIds, CancellationToken ct = default)
        {
            var ids = folderIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, Folder>();

            return await _context.Folders
                .AsNoTracking()
                .Where(f => ids.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, ct);
        }

        public async Task<IReadOnlyList<Guid>> GetNonWipFolderIdsAsync(
            Guid projectId, CancellationToken ct = default)
        {
            return await _context.Folders
                .AsNoTracking()
                .Where(f => f.ProjectId == projectId && f.Area != CdeArea.Wip)
                .Select(f => f.Id)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyDictionary<Guid, FileVersionState>> GetVersionsByIdsAsync(
            IEnumerable<Guid> versionIds, CancellationToken ct = default)
        {
            var ids = versionIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, FileVersionState>();

            return await _context.FileVersionStates
                .AsNoTracking()
                .Where(v => ids.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, ct);
        }

        public async Task<IReadOnlyDictionary<Guid, string>> GetAccountNamesAsync(
            IEnumerable<Guid> accountIds, CancellationToken ct = default)
        {
            var ids = accountIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, string>();

            return await _context.Accounts
                .AsNoTracking()
                .Where(a => ids.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.UserName, ct);
        }
    }
}
