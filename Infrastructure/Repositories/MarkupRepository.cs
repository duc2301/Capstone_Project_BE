using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.File;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class MarkupRepository : IMarkupRepository
    {
        private readonly CDESystemDbContext _context;

        public MarkupRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileItemId, ct);
        }

        public async Task<IReadOnlyDictionary<Guid, FileItem>> GetFileItemsAsync(
            IEnumerable<Guid> fileItemIds, CancellationToken ct = default)
        {
            var ids = fileItemIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, FileItem>();

            return await _context.FileItems
                .AsNoTracking()
                .Where(f => ids.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, ct);
        }

        public async Task<Guid?> GetProjectIdByFolderAsync(Guid folderId, CancellationToken ct = default)
        {
            return await _context.Folders
                .AsNoTracking()
                .Where(f => f.Id == folderId)
                .Select(f => (Guid?)f.ProjectId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<FileVersionState?> GetVersionAsync(Guid versionId, CancellationToken ct = default)
        {
            return await _context.FileVersionStates
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == versionId, ct);
        }

        public async Task<IReadOnlyDictionary<Guid, int>> GetVersionNumbersAsync(
            IEnumerable<Guid> versionIds, CancellationToken ct = default)
        {
            var ids = versionIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, int>();

            return await _context.FileVersionStates
                .AsNoTracking()
                .Where(v => ids.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.WorkingVersion, ct);
        }

        public async Task<MarkupSet?> GetSetForUpdateAsync(Guid setId, CancellationToken ct = default)
        {
            return await _context.MarkupSets
                .FirstOrDefaultAsync(s => s.Id == setId, ct);
        }

        public async Task<IReadOnlyList<MarkupSet>> GetSetsByFileAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.MarkupSets
                .AsNoTracking()
                .Where(s => s.FileItemId == fileItemId)
                .OrderByDescending(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<MarkupSet>> GetSetsByIssueAsync(
            Guid issueId, CancellationToken ct = default)
        {
            return await _context.MarkupSets
                .AsNoTracking()
                .Where(s => s.IssueId == issueId)
                .OrderByDescending(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .ToListAsync(ct);
        }

        public async Task<FileNote?> GetNoteForUpdateAsync(Guid noteId, CancellationToken ct = default)
        {
            return await _context.FileNotes
                .FirstOrDefaultAsync(n => n.Id == noteId, ct);
        }

        public async Task<IReadOnlyList<FileNote>> GetNotesBySetAsync(
            Guid setId, CancellationToken ct = default)
        {
            return await _context.FileNotes
                .AsNoTracking()
                .Where(n => n.MarkupSetId == setId)
                .OrderBy(n => n.CreatedAt)
                .ThenBy(n => n.Id)
                .ToListAsync(ct);
        }

        public async Task<MarkupNoteCounts> GetNoteCountsBySetAsync(
            Guid setId, CancellationToken ct = default)
        {
            var row = await _context.FileNotes
                .AsNoTracking()
                .Where(n => n.MarkupSetId == setId)
                .GroupBy(n => n.MarkupSetId)
                .Select(g => new MarkupNoteCounts
                {
                    Total = g.Count(),
                    Open = g.Count(n => n.Status == FileNoteStatus.Open)
                })
                .FirstOrDefaultAsync(ct);

            return row ?? new MarkupNoteCounts();
        }

        public async Task<IReadOnlyDictionary<Guid, MarkupNoteCounts>> GetNoteCountsBySetsAsync(
            IEnumerable<Guid> setIds, CancellationToken ct = default)
        {
            var ids = setIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, MarkupNoteCounts>();

            var rows = await _context.FileNotes
                .AsNoTracking()
                .Where(n => ids.Contains(n.MarkupSetId))
                .GroupBy(n => n.MarkupSetId)
                .Select(g => new
                {
                    SetId = g.Key,
                    Total = g.Count(),
                    Open = g.Count(n => n.Status == FileNoteStatus.Open)
                })
                .ToListAsync(ct);

            return rows.ToDictionary(
                r => r.SetId,
                r => new MarkupNoteCounts { Total = r.Total, Open = r.Open });
        }

        public async Task<IReadOnlyList<Guid>> GetNoteAuthorIdsBySetAsync(
            Guid setId, CancellationToken ct = default)
        {
            return await _context.FileNotes
                .AsNoTracking()
                .Where(n => n.MarkupSetId == setId && n.AuthorAccountId != null)
                .Select(n => n.AuthorAccountId!.Value)
                .Distinct()
                .ToListAsync(ct);
        }

        public async Task<string?> GetAccountNameAsync(Guid accountId, CancellationToken ct = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.Id == accountId)
                .Select(a => a.UserName)
                .FirstOrDefaultAsync(ct);
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
