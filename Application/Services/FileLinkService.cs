using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;

namespace Application.Services
{
    public class FileLinkService : IFileLinkService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderTreeRepository _folderTreeRepository;
        private readonly IPermissionCheckingRepository _permissionRepository;
        private readonly IFileZoneResolverService _zoneResolver;

        public FileLinkService(
            IUnitOfWork unitOfWork,
            IFolderTreeRepository folderTreeRepository,
            IPermissionCheckingRepository permissionRepository,
            IFileZoneResolverService zoneResolver)
        {
            _unitOfWork = unitOfWork;
            _folderTreeRepository = folderTreeRepository;
            _permissionRepository = permissionRepository;
            _zoneResolver = zoneResolver;
        }

        public async Task<RelatedFilesResponseDTO> GetRelatedFilesAsync(
            Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var source = await GetFileItemAsync(fileItemId);
            var sourceFolder = await GetFolderAsync(source.FolderId);

            if (!await CanViewFolderAsync(sourceFolder, actorId, isSystemAdmin))
                throw new ApiExceptionResponse("You do not have permission to view this file.", 403);

            var canLink = await CanModifyLinksAsync(sourceFolder, actorId, isSystemAdmin);
            var files = await BuildRelatedFileDtosAsync(fileItemId, sourceFolder, actorId, isSystemAdmin);

            return new RelatedFilesResponseDTO { CanLink = canLink, Files = files };
        }

        private async Task<List<RelatedFileDTO>> BuildRelatedFileDtosAsync(
            Guid fileItemId, Folder sourceFolder, Guid actorId, bool isSystemAdmin)
        {
            var links = await GetLinksOfAsync(fileItemId);
            if (links.Count == 0) return new List<RelatedFileDTO>();

            var linkByOtherId = links.ToDictionary(l => OtherEndOf(l, fileItemId));

            var relatedFiles = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => linkByOtherId.Keys.Contains(f.Id)))
                .ToList();
            if (relatedFiles.Count == 0) return new List<RelatedFileDTO>();

            var visibleFolderIds = await ResolveViewableFolderIdsAsync(sourceFolder.ProjectId, actorId, isSystemAdmin);
            if (visibleFolderIds != null)
                relatedFiles = relatedFiles.Where(f => visibleFolderIds.Contains(f.FolderId)).ToList();
            if (relatedFiles.Count == 0) return new List<RelatedFileDTO>();

            var foldersById = await GetFoldersByIdAsync(relatedFiles.Select(f => f.FolderId));
            var versionsById = await GetCurrentVersionsByIdAsync(relatedFiles);
            var accountNamesById = await GetAccountNamesAsync(
                links.Select(l => l.CreatedByAccountId).Where(id => id.HasValue).Select(id => id!.Value));

            return relatedFiles
                .Select(f =>
                {
                    var link = linkByOtherId[f.Id];
                    var folder = foldersById[f.FolderId];
                    var version = ResolveCurrentVersion(f, versionsById);

                    return new RelatedFileDTO
                    {
                        Id = f.Id,
                        Name = f.Name,
                        FileType = f.FileType,
                        Status = f.Status,
                        FolderId = f.FolderId,
                        FolderName = folder.Name,
                        Area = folder.Area,
                        CurrentVersionNumber = version?.VersionNumber ?? 0,
                        Format = version?.Format,
                        SizeBytes = version?.FileSizeBytes ?? 0,
                        LinkedAt = link.CreatedAt,
                        LinkedByName = link.CreatedByAccountId.HasValue
                                       && accountNamesById.TryGetValue(link.CreatedByAccountId.Value, out var name)
                            ? name
                            : null
                    };
                })
                .OrderBy(dto => dto.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<IEnumerable<LinkableFileDTO>> GetLinkableFilesAsync(
            Guid folderId, Guid? excludeFileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var folder = await GetFolderAsync(folderId);

            await RequireCanModifyLinksAsync(folder, actorId, isSystemAdmin);

            var scopeFolderIds = await ResolveScopeFolderIdsAsync(folder, actorId, isSystemAdmin);
            if (scopeFolderIds.Count == 0) return Enumerable.Empty<LinkableFileDTO>();

            var candidates = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => scopeFolderIds.Contains(f.FolderId)))
                .Where(f => !excludeFileItemId.HasValue || f.Id != excludeFileItemId.Value)
                .ToList();
            if (candidates.Count == 0) return Enumerable.Empty<LinkableFileDTO>();

            var linkedIds = excludeFileItemId.HasValue
                ? (await GetLinksOfAsync(excludeFileItemId.Value))
                    .Select(l => OtherEndOf(l, excludeFileItemId.Value))
                    .ToHashSet()
                : new HashSet<Guid>();

            var foldersById = await GetFoldersByIdAsync(candidates.Select(f => f.FolderId));
            var versionsById = await GetCurrentVersionsByIdAsync(candidates);

            return candidates
                .Select(f =>
                {
                    var version = ResolveCurrentVersion(f, versionsById);
                    return new LinkableFileDTO
                    {
                        Id = f.Id,
                        Name = f.Name,
                        FileType = f.FileType,
                        FolderId = f.FolderId,
                        FolderName = foldersById[f.FolderId].Name,
                        CurrentVersionNumber = version?.VersionNumber ?? 0,
                        Format = version?.Format,
                        SizeBytes = version?.FileSizeBytes ?? 0,
                        UpdatedAt = f.UpdatedAt,
                        AlreadyLinked = linkedIds.Contains(f.Id)
                    };
                })
                .OrderBy(dto => dto.FolderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(dto => dto.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<RelatedFilesResponseDTO> AddLinksAsync(
            Guid fileItemId, IReadOnlyCollection<Guid> relatedFileItemIds, Guid actorId, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var source = await GetFileItemAsync(fileItemId);
            var sourceFolder = await GetFolderAsync(source.FolderId);

            await RequireCanModifyLinksAsync(sourceFolder, actorId, isSystemAdmin);
            await StageLinksAsync(fileItemId, sourceFolder, relatedFileItemIds, actorId, isSystemAdmin);
            await _unitOfWork.CommitAsync();

            return await GetRelatedFilesAsync(fileItemId, actorId, isSystemAdmin, ct);
        }

        public async Task StageLinksOnUploadAsync(
            Guid fileItemId, Folder targetFolder, IReadOnlyCollection<Guid> relatedFileItemIds,
            Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
            => await StageLinksAsync(fileItemId, targetFolder, relatedFileItemIds, actorId, isSystemAdmin);

        public async Task RemoveLinkAsync(
            Guid fileItemId, Guid linkedFileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var source = await GetFileItemAsync(fileItemId);
            var sourceFolder = await GetFolderAsync(source.FolderId);

            await RequireCanModifyLinksAsync(sourceFolder, actorId, isSystemAdmin);

            var (first, second) = NormalizePair(fileItemId, linkedFileItemId);
            var link = (await _unitOfWork.Repository<FileLink>()
                    .FindAsync(l => l.FileItemId == first && l.LinkedFileItemId == second))
                .FirstOrDefault()
                ?? throw new ApiExceptionResponse("File link not found.", 404);

            _unitOfWork.Repository<FileLink>().Delete(link);
            await _unitOfWork.CommitAsync();
        }

        private async Task StageLinksAsync(
            Guid fileItemId, Folder sourceFolder, IReadOnlyCollection<Guid> relatedFileItemIds,
            Guid actorId, bool isSystemAdmin)
        {
            var targetIds = relatedFileItemIds.Distinct().Where(id => id != fileItemId).ToList();
            if (targetIds.Count == 0) return;

            var scopeFolderIds = await ResolveScopeFolderIdsAsync(sourceFolder, actorId, isSystemAdmin);

            var targets = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => targetIds.Contains(f.Id)))
                .ToList();

            var missing = targetIds.Except(targets.Select(f => f.Id)).ToList();
            if (missing.Count > 0)
                throw new ApiExceptionResponse($"Related file not found: {string.Join(", ", missing)}.", 404);

            var outOfScope = targets.Where(f => !scopeFolderIds.Contains(f.FolderId)).ToList();
            if (outOfScope.Count > 0)
                throw new ApiExceptionResponse(
                    "Chỉ được liên kết với tệp trong thư mục của nhóm ở cùng khu vực và bạn có quyền xem: "
                    + string.Join(", ", outOfScope.Select(f => f.Name)) + ".", 403);

            var existingPairs = (await GetLinksOfAsync(fileItemId))
                .Select(l => OtherEndOf(l, fileItemId))
                .ToHashSet();

            var now = DateTime.UtcNow;
            foreach (var target in targets)
            {
                if (!existingPairs.Add(target.Id)) continue;

                var (first, second) = NormalizePair(fileItemId, target.Id);
                await _unitOfWork.Repository<FileLink>().CreateAsync(new FileLink
                {
                    Id = Guid.NewGuid(),
                    FileItemId = first,
                    LinkedFileItemId = second,
                    CreatedByAccountId = actorId,
                    CreatedAt = now
                });
            }
        }

        private async Task<List<FileLink>> GetLinksOfAsync(Guid fileItemId)
            => (await _unitOfWork.Repository<FileLink>()
                    .FindAsync(l => l.FileItemId == fileItemId || l.LinkedFileItemId == fileItemId))
                .ToList();

        private static Guid OtherEndOf(FileLink link, Guid fileItemId)
            => link.FileItemId == fileItemId ? link.LinkedFileItemId : link.FileItemId;

        private static (Guid First, Guid Second) NormalizePair(Guid a, Guid b)
            => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

        private async Task<HashSet<Guid>> ResolveScopeFolderIdsAsync(
            Folder anchorFolder, Guid actorId, bool isSystemAdmin)
        {
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(anchorFolder.ProjectId);

            var teamFolder = _zoneResolver.ResolveTeamFolder(anchorFolder, projectFolders);
            if (teamFolder == null) return new HashSet<Guid>();

            var scope = CollectSubtreeFolderIds(teamFolder, projectFolders);

            var viewableFolderIds = await ResolveViewableFolderIdsAsync(anchorFolder.ProjectId, actorId, isSystemAdmin);
            if (viewableFolderIds != null) scope.IntersectWith(viewableFolderIds);

            return scope;
        }

        private async Task<HashSet<Guid>?> ResolveViewableFolderIdsAsync(
            Guid projectId, Guid actorId, bool isSystemAdmin)
        {
            if (isSystemAdmin || await _folderTreeRepository.HasFullAccessAsync(projectId, actorId))
                return null;

            return await _folderTreeRepository.GetViewableFolderIdsAsync(projectId, actorId);
        }

        private static HashSet<Guid> CollectSubtreeFolderIds(Folder root, IReadOnlyCollection<Folder> projectFolders)
        {
            var childrenByParentId = projectFolders
                .Where(f => f.ParentFolderId.HasValue)
                .GroupBy(f => f.ParentFolderId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var subtreeIds = new HashSet<Guid>();
            var pending = new Stack<Folder>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!subtreeIds.Add(current.Id)) continue;

                if (childrenByParentId.TryGetValue(current.Id, out var children))
                    foreach (var child in children) pending.Push(child);
            }

            return subtreeIds;
        }

        private async Task<bool> CanViewFolderAsync(Folder folder, Guid actorId, bool isSystemAdmin)
            => isSystemAdmin
               || await _folderTreeRepository.HasFullAccessAsync(folder.ProjectId, actorId)
               || await _folderTreeRepository.CanViewFolderAsync(folder.Id, actorId);

        private async Task<bool> CanModifyLinksAsync(Folder folder, Guid actorId, bool isSystemAdmin)
        {
            if (isSystemAdmin || await _folderTreeRepository.HasFullAccessAsync(folder.ProjectId, actorId))
                return true;

            var permission = await _permissionRepository.GetUserFolderPermissionAsync(folder.Id, actorId);
            return permission is { CanEdit: true } or { CanUpdate: true };
        }

        private async Task RequireCanModifyLinksAsync(Folder folder, Guid actorId, bool isSystemAdmin)
        {
            if (!await CanModifyLinksAsync(folder, actorId, isSystemAdmin))
                throw new ApiExceptionResponse(
                    "Bạn cần quyền Sửa hoặc Cập nhật trên thư mục này để thay đổi tệp liên quan.", 403);
        }

        private async Task<Dictionary<Guid, Folder>> GetFoldersByIdAsync(IEnumerable<Guid> folderIds)
        {
            var ids = folderIds.Distinct().ToList();
            return (await _unitOfWork.Repository<Folder>().FindAsync(f => ids.Contains(f.Id)))
                .ToDictionary(f => f.Id);
        }

        private async Task<Dictionary<Guid, FileVersion>> GetCurrentVersionsByIdAsync(
            IReadOnlyCollection<FileItem> files)
        {
            var versionIds = files
                .Where(f => f.CurrentVersionId.HasValue)
                .Select(f => f.CurrentVersionId!.Value)
                .Distinct()
                .ToList();
            if (versionIds.Count == 0) return new Dictionary<Guid, FileVersion>();

            return (await _unitOfWork.Repository<FileVersion>().FindAsync(v => versionIds.Contains(v.Id)))
                .ToDictionary(v => v.Id);
        }

        private static FileVersion? ResolveCurrentVersion(
            FileItem file, IReadOnlyDictionary<Guid, FileVersion> versionsById)
            => file.CurrentVersionId.HasValue && versionsById.TryGetValue(file.CurrentVersionId.Value, out var version)
                ? version
                : null;

        private async Task<Dictionary<Guid, string>> GetAccountNamesAsync(IEnumerable<Guid> accountIds)
        {
            var ids = accountIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<Guid, string>();

            return (await _unitOfWork.Repository<Account>().FindAsync(a => ids.Contains(a.Id)))
                .ToDictionary(a => a.Id, a => a.UserName);
        }

        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        private async Task<Folder> GetFolderAsync(Guid folderId)
            => await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
               ?? throw new ApiExceptionResponse("Folder not found.", 404);
    }
}
