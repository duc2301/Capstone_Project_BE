using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    public interface ILoiCheckRepository
    {
        Task<FileVersionLoiCheck?> GetCheckByFileVersionAsync(Guid fileVersionId, CancellationToken ct = default);

        Task<FileVersionLoiCheck?> GetCheckByFileVersionForUpdateAsync(Guid fileVersionId, CancellationToken ct = default);

        Task<IReadOnlyList<FileVersionLoiCheck>> GetUnfinishedChecksForUpdateAsync(CancellationToken ct = default);

        Task<FileVersionState?> GetVersionAsync(Guid fileVersionId, CancellationToken ct = default);

        Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default);

        Task<Guid?> GetProjectIdByFileItemAsync(Guid fileItemId, CancellationToken ct = default);

        Task<Guid?> GetProjectRuleSetIdAsync(Guid projectId, CancellationToken ct = default);

        Task<Guid?> GetDefaultRuleSetIdAsync(CancellationToken ct = default);

        Task<IReadOnlyList<LoiRequirement>> GetRequirementsAsync(Guid ruleSetId, CancellationToken ct = default);

        Task<IReadOnlyList<LoiComponent>> GetComponentsAsync(Guid ruleSetId, CancellationToken ct = default);

        Task<IReadOnlyList<LoiFieldAlias>> GetAliasesForProjectAsync(Guid? projectId, CancellationToken ct = default);
    }
}
