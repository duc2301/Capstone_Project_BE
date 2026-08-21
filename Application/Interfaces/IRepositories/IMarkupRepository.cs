using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    public class MarkupNoteCounts
    {
        public int Total { get; set; }
        public int Open { get; set; }
    }

    public interface IMarkupRepository
    {
        Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<Guid, FileItem>> GetFileItemsAsync(
            IEnumerable<Guid> fileItemIds, CancellationToken ct = default);

        Task<Guid?> GetProjectIdByFolderAsync(Guid folderId, CancellationToken ct = default);

        Task<FileVersionState?> GetVersionAsync(Guid versionId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<Guid, int>> GetVersionNumbersAsync(
            IEnumerable<Guid> versionIds, CancellationToken ct = default);

        Task<MarkupSet?> GetSetForUpdateAsync(Guid setId, CancellationToken ct = default);

        Task<IReadOnlyList<MarkupSet>> GetSetsByFileAsync(Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<MarkupSet>> GetSetsByIssueAsync(Guid issueId, CancellationToken ct = default);

        Task<FileNote?> GetNoteForUpdateAsync(Guid noteId, CancellationToken ct = default);

        Task<IReadOnlyList<FileNote>> GetNotesBySetAsync(Guid setId, CancellationToken ct = default);

        Task<MarkupNoteCounts> GetNoteCountsBySetAsync(Guid setId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<Guid, MarkupNoteCounts>> GetNoteCountsBySetsAsync(
            IEnumerable<Guid> setIds, CancellationToken ct = default);

        Task<IReadOnlyList<Guid>> GetNoteAuthorIdsBySetAsync(Guid setId, CancellationToken ct = default);

        Task<string?> GetAccountNameAsync(Guid accountId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<Guid, string>> GetAccountNamesAsync(
            IEnumerable<Guid> accountIds, CancellationToken ct = default);
    }
}
