using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Loi;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class LoiCheckRepository : ILoiCheckRepository
    {
        private readonly CDESystemDbContext _context;

        public LoiCheckRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<FileVersionLoiCheck?> GetCheckByFileVersionAsync(
            Guid fileVersionId, CancellationToken ct = default)
        {
            return await _context.FileVersionLoiChecks
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.FileVersionId == fileVersionId, ct);
        }

        public async Task<FileVersionLoiCheck?> GetCheckByFileVersionForUpdateAsync(
            Guid fileVersionId, CancellationToken ct = default)
        {
            return await _context.FileVersionLoiChecks
                .FirstOrDefaultAsync(c => c.FileVersionId == fileVersionId, ct);
        }

        public async Task<IReadOnlyList<FileVersionLoiCheck>> GetUnfinishedChecksForUpdateAsync(
            CancellationToken ct = default)
        {
            return await _context.FileVersionLoiChecks
                .Where(c => c.Status == LoiCheckStatus.Pending || c.Status == LoiCheckStatus.Processing)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .ToListAsync(ct);
        }

        public async Task<FileVersionState?> GetVersionAsync(Guid fileVersionId, CancellationToken ct = default)
        {
            return await _context.FileVersionStates
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == fileVersionId, ct);
        }

        public async Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileItemId, ct);
        }

        public async Task<Guid?> GetProjectIdByFileItemAsync(Guid fileItemId, CancellationToken ct = default)
        {
            return await _context.FileItems
                .AsNoTracking()
                .Where(f => f.Id == fileItemId)
                .Join(_context.Folders, f => f.FolderId, folder => folder.Id, (f, folder) => (Guid?)folder.ProjectId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<Guid?> GetProjectRuleSetIdAsync(Guid projectId, CancellationToken ct = default)
        {
            return await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => p.LoiRuleSetId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<LoiRequirement>> GetRequirementsAsync(
            Guid ruleSetId, CancellationToken ct = default)
        {
            return await _context.LoiRequirements
                .AsNoTracking()
                .Where(r => r.RuleSetId == ruleSetId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<LoiComponent>> GetComponentsAsync(
            Guid ruleSetId, CancellationToken ct = default)
        {
            return await _context.LoiComponents
                .AsNoTracking()
                .Where(c => c.RuleSetId == ruleSetId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<LoiFieldAlias>> GetAliasesForProjectAsync(
            Guid? projectId, CancellationToken ct = default)
        {
            return await _context.LoiFieldAliases
                .AsNoTracking()
                .Where(a => a.ProjectId == null || a.ProjectId == projectId)
                .ToListAsync(ct);
        }
    }
}
