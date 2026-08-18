using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.File;
using Domain.Enum.Issue;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FileDeletionRepository : IFileDeletionRepository
    {
        private readonly CDESystemDbContext _context;

        public FileDeletionRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<FileItem?> GetFileItemForUpdateAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileItems
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

        public async Task<Guid?> GetProjectManagerIdAsync(Guid projectId, CancellationToken ct = default)
        {
            return await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => p.ManagerAccountId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<int> CountOpenIssuesAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.Issues
                .AsNoTracking()
                .CountAsync(i => i.LinkedFileItemId == fileItemId && i.Status != IssueStatus.Closed, ct);
        }

        public async Task<bool> HasPendingApprovalAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.ApprovalRequests
                .AsNoTracking()
                .AnyAsync(a => a.FileItemId == fileItemId && a.Status == ApprovalRequestStatus.Pending, ct);
        }

        public async Task<bool> HasAnyApprovalAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.ApprovalRequests
                .AsNoTracking()
                .AnyAsync(a => a.FileItemId == fileItemId, ct);
        }

        public async Task<bool> HasReturnRequestAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.ZoneReturnRequests
                .AsNoTracking()
                .AnyAsync(r => r.FileItemId == fileItemId, ct);
        }

        public async Task<IReadOnlyList<FileVersionLoiCheck>> GetLoiChecksForDeleteAsync(
            Guid versionId, CancellationToken ct = default)
        {
            return await _context.FileVersionLoiChecks
                .Where(c => c.FileVersionId == versionId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<MarkupSet>> GetMarkupSetsForDeleteAsync(
            Guid versionId, CancellationToken ct = default)
        {
            return await _context.MarkupSets
                .Where(s => s.FileVersionId == versionId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<FileNote>> GetNotesForDeleteAsync(
            Guid versionId, IReadOnlyCollection<Guid> markupSetIds, CancellationToken ct = default)
        {
            var setIds = markupSetIds.ToList();

            return await _context.FileNotes
                .Where(n => n.FileVersionId == versionId || setIds.Contains(n.MarkupSetId))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Issue>> GetLinkedIssuesForUpdateAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.Issues
                .Where(i => i.LinkedFileItemId == fileItemId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<FileLink>> GetLinksForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileLinks
                .Where(l => l.FileItemId == fileItemId || l.LinkedFileItemId == fileItemId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<FilePermission>> GetFilePermissionsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FilePermissions
                .Where(p => p.FileItemId == fileItemId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<FileNamingMetadata>> GetNamingMetadataForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileNamingMetadata
                .Where(m => m.FileItemId == fileItemId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<FileSignaturePosition>> GetSignaturePositionsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileSignaturePositions
                .Where(p => p.FileItemId == fileItemId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<FileViewGrant>> GetViewGrantsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileViewGrants
                .Where(g => g.FileItemId == fileItemId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Document>> GetDocumentsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.Documents
                .Where(d => d.FileItemId == fileItemId)
                .ToListAsync(ct);
        }
    }
}
