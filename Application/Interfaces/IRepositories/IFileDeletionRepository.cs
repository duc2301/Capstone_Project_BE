using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    public interface IFileDeletionRepository
    {
        Task<FileItem?> GetFileItemForUpdateAsync(Guid fileItemId, CancellationToken ct = default);

        Task<FileVersionState?> GetVersionForUpdateAsync(Guid versionId, CancellationToken ct = default);

        Task<Folder?> GetFolderAsync(Guid folderId, CancellationToken ct = default);

        Task<Guid?> GetProjectManagerIdAsync(Guid projectId, CancellationToken ct = default);

        Task<int> CountOpenIssuesAsync(Guid fileItemId, CancellationToken ct = default);

        Task<bool> HasPendingApprovalAsync(Guid fileItemId, CancellationToken ct = default);

        Task<bool> HasAnyApprovalAsync(Guid fileItemId, CancellationToken ct = default);

        Task<bool> HasReturnRequestAsync(Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<FileVersionLoiCheck>> GetLoiChecksForDeleteAsync(
            Guid versionId, CancellationToken ct = default);

        Task<IReadOnlyList<MarkupSet>> GetMarkupSetsForDeleteAsync(
            Guid versionId, CancellationToken ct = default);

        Task<IReadOnlyList<FileNote>> GetNotesForDeleteAsync(
            Guid versionId, IReadOnlyCollection<Guid> markupSetIds, CancellationToken ct = default);

        Task<IReadOnlyList<Issue>> GetLinkedIssuesForUpdateAsync(
            Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<FileLink>> GetLinksForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<FilePermission>> GetFilePermissionsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<FileNamingMetadata>> GetNamingMetadataForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<FileSignaturePosition>> GetSignaturePositionsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<FileViewGrant>> GetViewGrantsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default);

        Task<IReadOnlyList<Document>> GetDocumentsForDeleteAsync(
            Guid fileItemId, CancellationToken ct = default);
    }
}
