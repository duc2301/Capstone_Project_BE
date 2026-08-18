using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    public interface IFileViewRepository
    {
        Task<FileItem?> GetFileItemAsync(Guid fileItemId, CancellationToken ct = default);

        Task<FileVersionState?> GetVersionForUpdateAsync(Guid versionId, CancellationToken ct = default);

        Task<Folder?> GetFolderAsync(Guid folderId, CancellationToken ct = default);
    }
}
