using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Loi;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class LoiRuleRepository : ILoiRuleRepository
    {
        private readonly CDESystemDbContext _context;

        public LoiRuleRepository(CDESystemDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyDictionary<Guid, LoiRuleSetCounts>> GetRuleSetCountsAsync(CancellationToken ct = default)
        {
            var components = await _context.LoiComponents
                .AsNoTracking()
                .GroupBy(c => c.RuleSetId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var requirements = await _context.LoiRequirements
                .AsNoTracking()
                .GroupBy(r => r.RuleSetId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var parameters = await _context.LoiParameters
                .AsNoTracking()
                .GroupBy(p => p.RuleSetId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var projects = await _context.Projects
                .AsNoTracking()
                .Where(p => p.LoiRuleSetId != null)
                .GroupBy(p => p.LoiRuleSetId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var result = new Dictionary<Guid, LoiRuleSetCounts>();

            LoiRuleSetCounts Entry(Guid id)
            {
                if (!result.TryGetValue(id, out var counts))
                {
                    counts = new LoiRuleSetCounts();
                    result[id] = counts;
                }
                return counts;
            }

            foreach (var row in components) Entry(row.Key).ComponentCount = row.Count;
            foreach (var row in requirements) Entry(row.Key).RequirementCount = row.Count;
            foreach (var row in parameters) Entry(row.Key).ParameterCount = row.Count;
            foreach (var row in projects) Entry(row.Key).ProjectCount = row.Count;

            return result;
        }

        public async Task<IReadOnlyList<LoiComponent>> SearchComponentsAsync(
            Guid ruleSetId, LoiDiscipline? discipline, string? search, CancellationToken ct = default)
        {
            var query = _context.LoiComponents
                .AsNoTracking()
                .Where(c => c.RuleSetId == ruleSetId);

            if (discipline.HasValue)
                query = query.Where(c => c.Discipline == discipline.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c => EF.Functions.ILike(c.Name, $"%{term}%")
                                      || EF.Functions.ILike(c.Code, $"%{term}%"));
            }

            return await query.OrderBy(c => c.CodeNormalized).ToListAsync(ct);
        }

        public async Task<IReadOnlyDictionary<string, LoiComponentUsage>> GetComponentUsageAsync(
            Guid ruleSetId, CancellationToken ct = default)
        {
            var groups = await _context.LoiRequirements
                .AsNoTracking()
                .Where(r => r.RuleSetId == ruleSetId && r.ComponentCode != null)
                .Select(r => new { Code = r.ComponentCode!, r.Variant, r.FieldNameNormalized })
                .Distinct()
                .ToListAsync(ct);

            return groups
                .GroupBy(row => row.Code)
                .ToDictionary(
                    byCode => byCode.Key,
                    byCode => new LoiComponentUsage
                    {
                        RequirementCount = byCode.Count(),
                        VariantCount = byCode.Select(row => row.Variant).Distinct().Count()
                    });
        }

        public async Task<IReadOnlyList<LoiRequirement>> GetRequirementsByComponentAsync(
            Guid ruleSetId, string componentCode, CancellationToken ct = default)
        {
            return await _context.LoiRequirements
                .Where(r => r.RuleSetId == ruleSetId && r.ComponentCode == componentCode)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyDictionary<(LoiDiscipline Discipline, string NameNormalized), int>>
            GetParameterUsageAsync(Guid ruleSetId, CancellationToken ct = default)
        {
            var rows = await _context.LoiRequirements
                .AsNoTracking()
                .Where(r => r.RuleSetId == ruleSetId)
                .GroupBy(r => new { r.Discipline, r.ParamNameNormalized })
                .Select(g => new { g.Key.Discipline, g.Key.ParamNameNormalized, Count = g.Count() })
                .ToListAsync(ct);

            return rows.ToDictionary(r => (r.Discipline, r.ParamNameNormalized), r => r.Count);
        }

        public async Task<int> CountProjectsUsingAsync(Guid ruleSetId, CancellationToken ct = default)
        {
            return await _context.Projects
                .AsNoTracking()
                .CountAsync(p => p.LoiRuleSetId == ruleSetId, ct);
        }

        public async Task<int> CountProjectsInheritingDefaultAsync(CancellationToken ct = default)
        {
            return await _context.Projects
                .AsNoTracking()
                .CountAsync(p => p.LoiRuleSetId == null, ct);
        }

        public async Task<bool> ParamNameExistsAsync(string paramNameNormalized, CancellationToken ct = default)
        {
            return await _context.LoiParameters
                .AsNoTracking()
                .AnyAsync(p => p.NameNormalized == paramNameNormalized, ct);
        }
    }
}
