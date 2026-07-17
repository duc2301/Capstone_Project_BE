using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Group;
using Domain.Enum.Project;

namespace Application.Services
{
    public class FileZoneResolverService : IFileZoneResolverService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FileZoneResolverService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public string FormatZone(CdeArea zone)
            => zone == CdeArea.Wip ? "WIP" : zone.ToString();

        public async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId)
            => (await _unitOfWork.Repository<Folder>().FindAsync(
                    f => f.ProjectId == projectId && !f.IsTemplate))
                .ToList();

        public async Task<HashSet<Guid>> GetActiveLeaderGroupIdsAsync(Guid actorId)
            => (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => m.AccountId == actorId
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Select(m => m.GroupId)
                .ToHashSet();

        public async Task<IReadOnlyCollection<Guid>> ResolveFileTeamGroupIdsAsync(
            FileItem fileItem,
            Folder currentFolder,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var activeParticipants = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == currentFolder.ProjectId && p.Status == ProjectParticipantStatus.Active))
                .ToDictionary(p => p.Id, p => p.GroupId);
            if (activeParticipants.Count == 0)
                throw new ApiExceptionResponse("File project has no active team.", 400);

            var teamGroupIds = new HashSet<Guid>();

            var filePermissions = await _unitOfWork.Repository<FilePermission>().FindAsync(
                p => p.FileItemId == fileItem.Id && p.ProjectParticipantId.HasValue);
            foreach (var permission in filePermissions)
            {
                if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
                    teamGroupIds.Add(groupId);
            }

            var folderIds = ResolveFolderPathIds(currentFolder, projectFolders);
            var folderPermissions = await _unitOfWork.Repository<FolderPermission>().FindAsync(
                p => folderIds.Contains(p.FolderId) && p.ProjectParticipantId.HasValue);
            foreach (var permission in folderPermissions)
            {
                if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
                    teamGroupIds.Add(groupId);
            }

            return teamGroupIds.Count > 0
                ? teamGroupIds
                : activeParticipants.Values.ToHashSet();
        }

        public async Task<IReadOnlyCollection<Guid>> ResolveTeamGroupIdsByFolderNameAsync(
            Guid projectId,
            Folder folder,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var teamFolder = ResolveZoneTeamFolder(folder, projectFolders);
            if (teamFolder == null)
                return Array.Empty<Guid>();

            var activeGroupIds = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == projectId && p.Status == ProjectParticipantStatus.Active))
                .Select(p => p.GroupId)
                .ToHashSet();
            if (activeGroupIds.Count == 0)
                return Array.Empty<Guid>();

            return (await _unitOfWork.Repository<Group>().FindAsync(
                    g => activeGroupIds.Contains(g.Id) && g.Name == teamFolder.Name))
                .Select(g => g.Id)
                .ToList();
        }

        public Folder? ResolveTeamFolder(Folder folder, IReadOnlyCollection<Folder> projectFolders)
            => ResolveZoneTeamFolder(folder, projectFolders);

        public async Task RequireActiveTeamLeaderAsync(
            Guid actorId,
            IReadOnlyCollection<Guid> teamGroupIds,
            string message)
        {
            var leaderGroupIds = await GetActiveLeaderGroupIdsAsync(actorId);
            if (!teamGroupIds.Any(leaderGroupIds.Contains))
                throw new ApiExceptionResponse(message, 403);
        }

        public async Task<Folder> ResolveTargetFolderAsync(
            Folder currentFolder,
            CdeArea targetZone,
            IReadOnlyCollection<Guid> teamGroupIds,
            IReadOnlyCollection<Folder> projectFolders,
            string notFoundMessage)
        {
            // Ưu tiên bản chiếu (mirror) của vị trí hiện tại ở khu vực đích —
            // file ở WIP/TeamA/Drawings thì về Shared/TeamA/Drawings, không rơi về folder gốc của team.
            var mirrorTarget = ResolveMirrorTargetFolder(currentFolder, targetZone, projectFolders);
            if (mirrorTarget != null)
                return mirrorTarget;

            var participantIds = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == currentFolder.ProjectId
                         && p.Status == ProjectParticipantStatus.Active
                         && teamGroupIds.Contains(p.GroupId)))
                .Select(p => p.Id)
                .ToHashSet();

            var targetFolders = projectFolders
                .Where(f => f.Area == targetZone)
                .ToDictionary(f => f.Id);

            var permissionTargetFolder = await FindTargetFolderByPermissionAsync(targetFolders, participantIds);
            if (permissionTargetFolder != null)
                return permissionTargetFolder;

            var sameTeamFolder = await FindTargetFolderByTeamNameAsync(
                currentFolder,
                targetZone,
                teamGroupIds,
                projectFolders);
            if (sameTeamFolder != null)
                return sameTeamFolder;

            throw new ApiExceptionResponse(notFoundMessage, 404);
        }

        private async Task<Folder?> FindTargetFolderByPermissionAsync(
            IReadOnlyDictionary<Guid, Folder> targetFolders,
            IReadOnlyCollection<Guid> participantIds)
        {
            if (participantIds.Count == 0 || targetFolders.Count == 0)
                return null;

            var targetFolderIds = targetFolders.Keys.ToHashSet();
            var folderPermission = (await _unitOfWork.Repository<FolderPermission>().FindAsync(
                    p => targetFolderIds.Contains(p.FolderId)
                         && p.ProjectParticipantId.HasValue
                         && participantIds.Contains(p.ProjectParticipantId.Value)))
                .FirstOrDefault();

            return folderPermission != null && targetFolders.TryGetValue(folderPermission.FolderId, out var folder)
                ? folder
                : null;
        }

        private async Task<Folder?> FindTargetFolderByTeamNameAsync(
            Folder currentFolder,
            CdeArea targetZone,
            IReadOnlyCollection<Guid> teamGroupIds,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var targetRoot = projectFolders.FirstOrDefault(f => f.ParentFolderId == null && f.Area == targetZone);
            if (targetRoot == null)
                return null;

            var currentTeamFolder = ResolveZoneTeamFolder(currentFolder, projectFolders);
            if (currentTeamFolder != null)
            {
                var matchingFolder = projectFolders.FirstOrDefault(
                    f => f.ParentFolderId == targetRoot.Id
                         && string.Equals(f.Name, currentTeamFolder.Name, StringComparison.OrdinalIgnoreCase));
                if (matchingFolder != null)
                    return matchingFolder;
            }

            var teamNames = (await _unitOfWork.Repository<Group>().FindAsync(g => teamGroupIds.Contains(g.Id)))
                .Select(g => g.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return projectFolders.FirstOrDefault(
                f => f.ParentFolderId == targetRoot.Id && teamNames.Contains(f.Name));
        }

        // Tìm bản chiếu của folder hiện tại ở khu vực đích qua link MirrorSourceFolderId.
        // Mọi bản chiếu đều trỏ về folder WIP gốc: đang ở WIP thì lấy chính Id, ngoài WIP thì theo link ngược.
        // Trả null nếu chưa có bản chiếu (folder cũ) — caller rơi xuống các cách tìm cũ.
        private static Folder? ResolveMirrorTargetFolder(
            Folder currentFolder,
            CdeArea targetZone,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var wipSourceId = currentFolder.Area == CdeArea.Wip
                ? currentFolder.Id
                : currentFolder.MirrorSourceFolderId;

            if (targetZone == CdeArea.Wip)
            {
                var wipFolder = wipSourceId.HasValue
                    ? projectFolders.FirstOrDefault(f => f.Id == wipSourceId.Value && f.Area == CdeArea.Wip)
                    : null;
                return wipFolder ?? FindTargetFolderByPath(currentFolder, targetZone, projectFolders);
            }

            if (wipSourceId.HasValue)
            {
                var linked = projectFolders.FirstOrDefault(f =>
                    f.Area == targetZone && f.MirrorSourceFolderId == wipSourceId.Value);
                if (linked != null)
                    return linked;
            }

            // Folder chưa có link mirror: dò theo đường dẫn tên cùng vị trí ở khu vực đích.
            return FindTargetFolderByPath(currentFolder, targetZone, projectFolders);
        }

        // Dò folder cùng đường dẫn tên (so từ root khu vực) ở khu vực đích. Trả null nếu thiếu cấp nào đó.
        private static Folder? FindTargetFolderByPath(
            Folder currentFolder,
            CdeArea targetZone,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var byId = projectFolders.ToDictionary(f => f.Id);

            // Chuỗi tên từ ngay dưới root nguồn xuống folder hiện tại.
            var names = new List<string>();
            var cur = (Folder?)currentFolder;
            while (cur is { ParentFolderId: not null })
            {
                names.Add(cur.Name);
                byId.TryGetValue(cur.ParentFolderId.Value, out cur);
            }
            if (cur == null)
                return null; // chuỗi cha bị đứt, không xác định được đường dẫn
            names.Reverse();

            var target = projectFolders.FirstOrDefault(f => f.ParentFolderId == null && f.Area == targetZone);
            foreach (var name in names)
            {
                if (target == null)
                    return null;
                target = projectFolders.FirstOrDefault(f =>
                    f.ParentFolderId == target.Id
                    && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            return target;
        }

        private static HashSet<Guid> ResolveFolderPathIds(Folder folder, IReadOnlyCollection<Folder> projectFolders)
        {
            var byId = projectFolders.ToDictionary(f => f.Id);
            var folderIds = new HashSet<Guid>();
            var current = folder;

            while (folderIds.Add(current.Id)
                   && current.ParentFolderId.HasValue
                   && byId.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                current = parent;
            }

            return folderIds;
        }

        private static Folder? ResolveZoneTeamFolder(Folder folder, IReadOnlyCollection<Folder> projectFolders)
        {
            var byId = projectFolders.ToDictionary(f => f.Id);
            var current = folder;
            Folder? teamFolder = null;

            while (current.ParentFolderId.HasValue
                   && byId.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                teamFolder = current;
                current = parent;
            }

            return current.ParentFolderId == null ? teamFolder : null;
        }
    }
}
