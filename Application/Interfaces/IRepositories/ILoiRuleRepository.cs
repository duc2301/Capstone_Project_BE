using Domain.Entities;
using Domain.Enum.Loi;

namespace Application.Interfaces.IRepositories
{
    public class LoiRuleSetCounts
    {
        public int ComponentCount { get; set; }
        public int RequirementCount { get; set; }
        public int ParameterCount { get; set; }
        public int ProjectCount { get; set; }
    }

    public class LoiComponentUsage
    {
        public int VariantCount { get; set; }
        public int RequirementCount { get; set; }
    }

    public interface ILoiRuleRepository
    {
        Task<IReadOnlyDictionary<Guid, LoiRuleSetCounts>> GetRuleSetCountsAsync(CancellationToken ct = default);

        Task<IReadOnlyList<LoiComponent>> SearchComponentsAsync(
            Guid ruleSetId, LoiDiscipline? discipline, string? search, CancellationToken ct = default);

        Task<IReadOnlyDictionary<string, LoiComponentUsage>> GetComponentUsageAsync(
            Guid ruleSetId, CancellationToken ct = default);

        Task<IReadOnlyList<LoiRequirement>> GetRequirementsByComponentAsync(
            Guid ruleSetId, string componentCode, CancellationToken ct = default);

        Task<IReadOnlyDictionary<(LoiDiscipline Discipline, string NameNormalized), int>> GetParameterUsageAsync(
            Guid ruleSetId, CancellationToken ct = default);

        Task<int> CountProjectsUsingAsync(Guid ruleSetId, CancellationToken ct = default);

        Task<int> CountProjectsInheritingDefaultAsync(CancellationToken ct = default);

        Task<bool> ParamNameExistsAsync(string paramNameNormalized, CancellationToken ct = default);

        Task<bool> RequirementParamExistsAsync(string paramNameNormalized, CancellationToken ct = default);

        Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct = default);

        Task<IReadOnlyList<LoiFieldAlias>> GetAliasesForProjectAsync(
            Guid projectId, CancellationToken ct = default);

        Task<LoiFieldAlias?> FindAliasAsync(
            string aliasNormalized, Guid projectId, CancellationToken ct = default);

        Task<LoiFieldAlias?> GetAliasForUpdateAsync(Guid aliasId, CancellationToken ct = default);
    }
}
