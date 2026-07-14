using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Interfaces.IServices
{
    public interface IFileZoneResolverService
    {
        string FormatZone(CdeArea zone);
        Task<List<Folder>> GetProjectFoldersAsync(Guid projectId);
        Task<HashSet<Guid>> GetActiveLeaderGroupIdsAsync(Guid actorId);
        Task<IReadOnlyCollection<Guid>> ResolveFileTeamGroupIdsAsync(
            FileItem fileItem,
            Folder currentFolder,
            IReadOnlyCollection<Folder> projectFolders);
        Task<IReadOnlyCollection<Guid>> ResolveTeamGroupIdsByFolderNameAsync(
            Guid projectId,
            Folder folder,
            IReadOnlyCollection<Folder> projectFolders);
        Task RequireActiveTeamLeaderAsync(
            Guid actorId,
            IReadOnlyCollection<Guid> teamGroupIds,
            string message);
        Task<Folder> ResolveTargetFolderAsync(
            Folder currentFolder,
            CdeArea targetZone,
            IReadOnlyCollection<Guid> teamGroupIds,
            IReadOnlyCollection<Folder> projectFolders,
            string notFoundMessage);
    }
}
