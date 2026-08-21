using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    public interface IFileLinkRepository
    {
        Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default);

        Task<Folder?> GetFolderAsync(Guid folderId, CancellationToken ct = default);

        Task<IReadOnlyList<FileLink>> GetLinksOfFileAsync(Guid fileItemId, CancellationToken ct = default);

        Task<FileLink?> FindLinkPairForUpdateAsync(Guid first, Guid second, CancellationToken ct = default);

        Task<IReadOnlyList<FileItem>> GetFileItemsByIdsAsync(
            IEnumerable<Guid> fileItemIds, CancellationToken ct = default);

        Task<IReadOnlyList<FileItem>> GetFileItemsInFoldersAsync(
            IEnumerable<Guid> folderIds, Guid? excludeFileItemId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<Guid, Folder>> GetFoldersByIdsAsync(
            IEnumerable<Guid> folderIds, CancellationToken ct = default);

        Task<IReadOnlyList<Guid>> GetNonWipFolderIdsAsync(Guid projectId, CancellationToken ct = default);

        Task<IReadOnlyDictionary<Guid, FileVersionState>> GetVersionsByIdsAsync(
            IEnumerable<Guid> versionIds, CancellationToken ct = default);

        Task<IReadOnlyDictionary<Guid, string>> GetAccountNamesAsync(
            IEnumerable<Guid> accountIds, CancellationToken ct = default);
    }
}
