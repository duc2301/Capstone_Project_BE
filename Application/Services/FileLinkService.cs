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
        private readonly IFileLinkRepository _links;
        private readonly IPermissionCheckingService _permission;
        private readonly IFileZoneResolverService _zoneResolver;

        public FileLinkService(
            IUnitOfWork unitOfWork,
            IFileLinkRepository links,
            IPermissionCheckingService permission,
            IFileZoneResolverService zoneResolver)
        {
            _unitOfWork = unitOfWork;
            _links = links;
            _permission = permission;
            _zoneResolver = zoneResolver;
        }

        public async Task<RelatedFilesResponseDTO> GetRelatedFilesAsync(
            Guid fileItemId, Guid actorId, CancellationToken ct = default)
        {
            var source = await GetFileItemAsync(fileItemId);
            var sourceFolder = await GetFolderAsync(source.FolderId);

            if (!await CanViewFolderAsync(sourceFolder, actorId))
                throw new ApiExceptionResponse("You do not have permission to view this file.", 403);

            var canLink = await CanModifyLinksAsync(sourceFolder, actorId);
            var files = await BuildRelatedFileDtosAsync(fileItemId, sourceFolder, actorId);

            return new RelatedFilesResponseDTO { CanLink = canLink, Files = files };
        }

        private async Task<List<RelatedFileDTO>> BuildRelatedFileDtosAsync(
            Guid fileItemId, Folder sourceFolder, Guid actorId)
        {
            var links = await GetLinksOfAsync(fileItemId);
            if (links.Count == 0) return new List<RelatedFileDTO>();

            var linkByOtherId = links.ToDictionary(l => OtherEndOf(l, fileItemId));

            var relatedFiles = (await _links.GetFileItemsByIdsAsync(linkByOtherId.Keys)).ToList();
            if (relatedFiles.Count == 0) return new List<RelatedFileDTO>();

            var visibleFolderIds = await ResolveViewableFolderIdsAsync(sourceFolder.ProjectId, actorId);
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
                        CurrentVersionNumber = version?.WorkingVersion ?? 0,
                        DisplayVersion = version?.DisplayVersion,
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
            Guid folderId, Guid? excludeFileItemId, Guid actorId, CancellationToken ct = default)
        {
            var folder = await GetFolderAsync(folderId);

            await RequireCanModifyLinksAsync(folder, actorId);

            var scopeFolderIds = await ResolveScopeFolderIdsAsync(folder, actorId);
            if (scopeFolderIds.Count == 0) return Enumerable.Empty<LinkableFileDTO>();

            var candidates = await _links.GetFileItemsInFoldersAsync(scopeFolderIds, excludeFileItemId, ct);
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
                        CurrentVersionNumber = version?.WorkingVersion ?? 0,
                        DisplayVersion = version?.DisplayVersion,
                        Format = version?.Format,
                        SizeBytes = version?.FileSizeBytes ?? 0,
                        CreatedAt = f.CreatedAt,
                        UpdatedAt = f.UpdatedAt,
                        AlreadyLinked = linkedIds.Contains(f.Id)
                    };
                })
                .OrderBy(dto => dto.FolderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(dto => dto.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<RelatedFilesResponseDTO> AddLinksAsync(
            Guid fileItemId, IReadOnlyCollection<Guid> relatedFileItemIds, Guid actorId,
            CancellationToken ct = default)
        {
            var source = await GetFileItemAsync(fileItemId);
            var sourceFolder = await GetFolderAsync(source.FolderId);

            await RequireCanModifyLinksAsync(sourceFolder, actorId);
            await StageLinksAsync(fileItemId, sourceFolder, relatedFileItemIds, actorId);
            await _unitOfWork.CommitAsync();

            return await GetRelatedFilesAsync(fileItemId, actorId, ct);
        }

        public async Task StageLinksOnUploadAsync(
            Guid fileItemId, Folder targetFolder, IReadOnlyCollection<Guid> relatedFileItemIds,
            Guid actorId, CancellationToken ct = default)
            => await StageLinksAsync(fileItemId, targetFolder, relatedFileItemIds, actorId);

        public async Task ValidateUploadLinkTargetsAsync(
            Folder targetFolder, IReadOnlyCollection<Guid> relatedFileItemIds,
            Guid actorId, CancellationToken ct = default)
        {
            var targetIds = relatedFileItemIds.Distinct().ToList();
            if (targetIds.Count == 0) return;
            // Chỉ kiểm phạm vi + quyền, KHÔNG ghi. Gọi TRƯỚC khi lưu file để lỗi thì chưa có file mồ côi
            // (hệ versioning mới commit FileItem giữa luồng nên không thể rollback bằng 1 commit cuối).
            await ResolveValidatedTargetsAsync(targetFolder, targetIds, actorId);
        }

        public async Task RemoveLinkAsync(
            Guid fileItemId, Guid linkedFileItemId, Guid actorId, CancellationToken ct = default)
        {
            var source = await GetFileItemAsync(fileItemId);
            var sourceFolder = await GetFolderAsync(source.FolderId);

            await RequireCanModifyLinksAsync(sourceFolder, actorId);

            var (first, second) = NormalizePair(fileItemId, linkedFileItemId);
            var link = await _links.FindLinkPairForUpdateAsync(first, second, ct)
                ?? throw new ApiExceptionResponse("File link not found.", 404);

            _unitOfWork.Repository<FileLink>().Delete(link);
            await _unitOfWork.CommitAsync();
        }

        private async Task StageLinksAsync(
            Guid fileItemId, Folder sourceFolder, IReadOnlyCollection<Guid> relatedFileItemIds,
            Guid actorId)
        {
            var targetIds = relatedFileItemIds.Distinct().Where(id => id != fileItemId).ToList();
            if (targetIds.Count == 0) return;

            var targets = await ResolveValidatedTargetsAsync(sourceFolder, targetIds, actorId);

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

        // Kiểm file đích tồn tại + nằm trong phạm vi cho phép (ô của nhóm ở cùng khu vực, giao quyền View).
        // Trả về danh sách FileItem đích đã kiểm; ném lỗi nếu thiếu hoặc ngoài phạm vi. KHÔNG ghi gì.
        private async Task<List<FileItem>> ResolveValidatedTargetsAsync(
            Folder sourceFolder, IReadOnlyCollection<Guid> targetIds, Guid actorId)
        {
            var scopeFolderIds = await ResolveScopeFolderIdsAsync(sourceFolder, actorId);

            var targets = (await _links.GetFileItemsByIdsAsync(targetIds)).ToList();

            var missing = targetIds.Except(targets.Select(f => f.Id)).ToList();
            if (missing.Count > 0)
                throw new ApiExceptionResponse($"Related file not found: {string.Join(", ", missing)}.", 404);

            var outOfScope = targets.Where(f => !scopeFolderIds.Contains(f.FolderId)).ToList();
            if (outOfScope.Count > 0)
                throw new ApiExceptionResponse(
                    "Chỉ được liên kết với tệp trong thư mục của nhóm ở cùng khu vực và bạn có quyền xem: "
                    + string.Join(", ", outOfScope.Select(f => f.Name)) + ".", 403);

            return targets;
        }

        private async Task<IReadOnlyList<FileLink>> GetLinksOfAsync(Guid fileItemId)
            => await _links.GetLinksOfFileAsync(fileItemId);

        private static Guid OtherEndOf(FileLink link, Guid fileItemId)
            => link.FileItemId == fileItemId ? link.LinkedFileItemId : link.FileItemId;

        private static (Guid First, Guid Second) NormalizePair(Guid a, Guid b)
            => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

        private async Task<HashSet<Guid>> ResolveScopeFolderIdsAsync(
            Folder anchorFolder, Guid actorId)
        {
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(anchorFolder.ProjectId);

            var teamFolder = _zoneResolver.ResolveTeamFolder(anchorFolder, projectFolders);
            if (teamFolder == null) return new HashSet<Guid>();

            var scope = CollectSubtreeFolderIds(teamFolder, projectFolders);

            var viewableFolderIds = await ResolveViewableFolderIdsAsync(anchorFolder.ProjectId, actorId);
            if (viewableFolderIds != null) scope.IntersectWith(viewableFolderIds);

            return scope;
        }

        // null = full access (no folder filter); otherwise the set of folders the user can View.
        private async Task<HashSet<Guid>?> ResolveViewableFolderIdsAsync(
            Guid projectId, Guid actorId)
        {
            if (await _permission.HasSystemAdminAsync(actorId))
                return null;

            var viewableFolderIds = await _permission.GetViewableFolderIdsAsync(projectId, actorId);
            if (!await _permission.HasProjectFullAccessAsync(projectId, actorId))
                return viewableFolderIds;

            var nonWipFolderIds = await _links.GetNonWipFolderIdsAsync(projectId);
            viewableFolderIds.UnionWith(nonWipFolderIds);

            return viewableFolderIds;
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

        private Task<bool> CanViewFolderAsync(Folder folder, Guid actorId)
            => _permission.HasViewFolderAsync(folder.Id, actorId);

        private Task<bool> CanModifyLinksAsync(Folder folder, Guid actorId)
            => _permission.HasEditFolderAsync(folder.Id, actorId);

        private async Task RequireCanModifyLinksAsync(Folder folder, Guid actorId)
        {
            if (!await CanModifyLinksAsync(folder, actorId))
                throw new ApiExceptionResponse(
                    "Bạn cần quyền Sửa hoặc Cập nhật trên thư mục này để thay đổi tệp liên quan.", 403);
        }

        private async Task<IReadOnlyDictionary<Guid, Folder>> GetFoldersByIdAsync(IEnumerable<Guid> folderIds)
            => await _links.GetFoldersByIdsAsync(folderIds);

        // Hệ versioning mới: version hiện hành nằm ở dòng FileVersionState mà CurrentVersionId trỏ tới.
        private async Task<IReadOnlyDictionary<Guid, FileVersionState>> GetCurrentVersionsByIdAsync(
            IReadOnlyCollection<FileItem> files)
        {
            var versionIds = files
                .Where(f => f.CurrentVersionId.HasValue)
                .Select(f => f.CurrentVersionId!.Value)
                .ToList();

            return await _links.GetVersionsByIdsAsync(versionIds);
        }

        private static FileVersionState? ResolveCurrentVersion(
            FileItem file, IReadOnlyDictionary<Guid, FileVersionState> versionsById)
            => file.CurrentVersionId.HasValue && versionsById.TryGetValue(file.CurrentVersionId.Value, out var version)
                ? version
                : null;

        private async Task<IReadOnlyDictionary<Guid, string>> GetAccountNamesAsync(IEnumerable<Guid> accountIds)
            => await _links.GetAccountNamesAsync(accountIds);

        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _links.GetFileItemAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        private async Task<Folder> GetFolderAsync(Guid folderId)
            => await _links.GetFolderAsync(folderId)
               ?? throw new ApiExceptionResponse("Folder not found.", 404);
    }
}
