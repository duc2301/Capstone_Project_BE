using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Services
{
    // Chuyển trạng thái CDE (ISO 19650). Copy cho Wip→Shared, Shared→Published; Move cho Published→Archived.
    public class FolderTransitionService : IFolderTransitionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IFolderPermissionService _permission;
        private readonly IFileStorageService _storage;

        public FolderTransitionService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IFolderPermissionService permission,
            IFileStorageService storage)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _permission = permission;
            _storage = storage;
        }

        // Snapshot dữ liệu dự án trong 1 lần promote (gồm cả entity mới tạo để lookup nội bộ).
        private sealed class Ctx
        {
            public Guid Actor;
            public Guid ProjectId;
            public List<Folder> Folders = new();
            public List<FileItem> Files = new();
            public List<FileVersion> Versions = new();
            public int FoldersCreated;
            public int FilesPromoted;
        }

        public async Task<TransitionResultDTO> PromoteFolderAsync(Guid folderId, CdeArea targetArea)
        {
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var source = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            if (source.ParentFolderId == null)
                throw new ApiExceptionResponse("Cannot transition a root area folder.", 400);

            ValidateStep(source.Area, targetArea);
            await RequireGateAsync(actor, source.Id, targetArea);

            var ctx = await BuildContextAsync(actor, source.ProjectId);
            var src = ctx.Folders.First(f => f.Id == source.Id);
            var target = await EnsureMirrorPathAsync(ctx, src, targetArea);

            if (IsMove(targetArea)) await MoveFolderRecursiveAsync(ctx, src, target, targetArea);
            else await CopyFolderRecursiveAsync(ctx, src, target, targetArea);

            await _unitOfWork.CommitAsync();
            return Result(target, targetArea, ctx);
        }

        public async Task<TransitionResultDTO> PromoteFileAsync(Guid fileItemId, CdeArea targetArea, Guid? versionId)
        {
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var sourceFolder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            if (sourceFolder.ParentFolderId == null)
                throw new ApiExceptionResponse("File is not inside a transitionable folder.", 400);

            ValidateStep(sourceFolder.Area, targetArea);
            await RequireGateAsync(actor, sourceFolder.Id, targetArea);

            var ctx = await BuildContextAsync(actor, sourceFolder.ProjectId);
            var srcFolder = ctx.Folders.First(f => f.Id == sourceFolder.Id);
            var srcFile = ctx.Files.First(f => f.Id == fileItemId);

            var version = versionId.HasValue
                ? ctx.Versions.FirstOrDefault(v => v.Id == versionId.Value && v.FileItemId == srcFile.Id)
                    ?? throw new ApiExceptionResponse("Version not found for this file.", 404)
                : CurrentVersionOf(ctx, srcFile)
                    ?? throw new ApiExceptionResponse("File has no content version.", 400);

            var target = await EnsureMirrorPathAsync(ctx, srcFolder, targetArea);

            if (IsMove(targetArea)) MoveFileInto(ctx, srcFile, target);
            else await CopyFileIntoAsync(ctx, srcFile, version, target);

            await _unitOfWork.CommitAsync();
            return Result(target, targetArea, ctx);
        }

        // ---------- luật chuyển ----------

        private static CdeArea? NextArea(CdeArea a) => a switch
        {
            CdeArea.Wip => CdeArea.Shared,
            CdeArea.Shared => CdeArea.Published,
            CdeArea.Published => CdeArea.Archived,
            _ => null,
        };

        private static bool IsMove(CdeArea target) => target == CdeArea.Archived;

        private static FolderAction GateFor(CdeArea target) => target switch
        {
            CdeArea.Shared => FolderAction.Update,    // bên sở hữu tự quyết chia sẻ
            CdeArea.Published => FolderAction.Approve, // PM phê duyệt phát hành
            CdeArea.Archived => FolderAction.Approve,  // PM thu hồi/lưu trữ
            _ => FolderAction.Edit,
        };

        private static void ValidateStep(CdeArea sourceArea, CdeArea targetArea)
        {
            var next = NextArea(sourceArea);
            if (next == null || targetArea != next.Value)
                throw new ApiExceptionResponse(
                    $"Invalid transition from {sourceArea}. Only forward one step is allowed" +
                    (next == null ? " (no further step)." : $": {sourceArea} → {next}."), 400);
        }

        private Task RequireGateAsync(Guid actor, Guid folderId, CdeArea targetArea)
            => _permission.RequireAsync(actor, folderId, GateFor(targetArea));

        // ---------- bối cảnh ----------

        private async Task<Ctx> BuildContextAsync(Guid actor, Guid projectId)
        {
            var folders = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == projectId)
                .ToList();
            var folderIds = folders.Select(f => f.Id).ToHashSet();

            var files = (await _unitOfWork.Repository<FileItem>().GetAllAsync())
                .Where(fi => folderIds.Contains(fi.FolderId))
                .ToList();
            var fileIds = files.Select(f => f.Id).ToHashSet();

            var versions = (await _unitOfWork.Repository<FileVersion>().GetAllAsync())
                .Where(v => fileIds.Contains(v.FileItemId))
                .ToList();

            return new Ctx { Actor = actor, ProjectId = projectId, Folders = folders, Files = files, Versions = versions };
        }

        // ---------- mirror cấu trúc ----------

        private Folder GetAreaRoot(Ctx ctx, CdeArea area)
            => ctx.Folders.FirstOrDefault(f => f.ParentFolderId == null && f.Area == area && !f.IsTemplate)
               ?? throw new ApiExceptionResponse($"Area root '{area}' is missing for this project.", 500);

        private IEnumerable<Folder> ChildrenOf(Ctx ctx, Guid parentId)
            => ctx.Folders.Where(f => f.ParentFolderId == parentId);

        private async Task<Folder> EnsureMirrorPathAsync(Ctx ctx, Folder source, CdeArea targetArea)
        {
            // Chuỗi từ root khu vực (loại trừ) xuống tới source: [groupFolder, sub1, …, source]
            var chain = new List<Folder>();
            var cur = source;
            while (cur.ParentFolderId != null)
            {
                chain.Add(cur);
                cur = ctx.Folders.First(f => f.Id == cur.ParentFolderId.Value);
            }
            chain.Reverse();

            var cursor = GetAreaRoot(ctx, targetArea);
            foreach (var f in chain)
                cursor = await EnsureMirrorChildAsync(ctx, cursor, f, targetArea);
            return cursor;
        }

        private async Task<Folder> EnsureMirrorChildAsync(Ctx ctx, Folder targetParent, Folder sourceChild, CdeArea targetArea)
        {
            var existing = ChildrenOf(ctx, targetParent.Id).FirstOrDefault(c =>
                string.Equals(c.Name, sourceChild.Name, StringComparison.OrdinalIgnoreCase)
                && c.OwnerGroupId == sourceChild.OwnerGroupId);
            if (existing != null) return existing;

            var now = DateTime.UtcNow;
            var created = new Folder
            {
                Id = Guid.NewGuid(),
                ProjectId = ctx.ProjectId,
                ParentFolderId = targetParent.Id,
                Name = sourceChild.Name,
                Area = targetArea,
                OwnerGroupId = sourceChild.OwnerGroupId,
                OwnerOrganizationId = sourceChild.OwnerOrganizationId,
                IsTemplate = false,
                CreatedByAccountId = ctx.Actor,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _unitOfWork.Repository<Folder>().CreateAsync(created);
            ctx.Folders.Add(created);
            ctx.FoldersCreated++;
            return created;
        }

        // ---------- copy ----------

        private async Task CopyFolderRecursiveAsync(Ctx ctx, Folder source, Folder target, CdeArea targetArea)
        {
            foreach (var file in FilesInFolder(ctx, source.Id).ToList())
            {
                var current = CurrentVersionOf(ctx, file);
                if (current != null) await CopyFileIntoAsync(ctx, file, current, target);
            }
            foreach (var childSrc in ChildrenOf(ctx, source.Id).Where(c => !c.IsTemplate).ToList())
            {
                var childTarget = await EnsureMirrorChildAsync(ctx, target, childSrc, targetArea);
                await CopyFolderRecursiveAsync(ctx, childSrc, childTarget, targetArea);
            }
        }

        private async Task CopyFileIntoAsync(Ctx ctx, FileItem sourceFile, FileVersion version, Folder targetFolder)
        {
            var stored = await CopyBlobAsync(version, targetFolder);
            var now = DateTime.UtcNow;
            var existing = FilesInFolder(ctx, targetFolder.Id)
                .FirstOrDefault(f => string.Equals(f.Name, sourceFile.Name, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                var item = new FileItem
                {
                    Id = Guid.NewGuid(),
                    FolderId = targetFolder.Id,
                    Name = sourceFile.Name,
                    FileType = sourceFile.FileType,
                    CreatedByAccountId = ctx.Actor,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                var v = NewVersionFrom(version, item.Id, 1, stored, ctx.Actor, now);
                item.CurrentVersionId = v.Id;
                await _unitOfWork.Repository<FileItem>().CreateAsync(item);
                await _unitOfWork.Repository<FileVersion>().CreateAsync(v);
                ctx.Files.Add(item);
                ctx.Versions.Add(v);
            }
            else
            {
                // Trùng tên ở đích -> tạo version mới, đẩy bản đích cũ vào Archived (giống luồng upload).
                var versions = VersionsOf(ctx, existing.Id).ToList();
                var nextNo = (versions.Count == 0 ? 0 : versions.Max(x => x.VersionNumber)) + 1;
                var v = NewVersionFrom(version, existing.Id, nextNo, stored, ctx.Actor, now);
                await _unitOfWork.Repository<FileVersion>().CreateAsync(v);
                ctx.Versions.Add(v);

                var oldCurrent = versions.FirstOrDefault(x => x.Id == existing.CurrentVersionId);
                existing.CurrentVersionId = v.Id;
                existing.UpdatedAt = now;
                if (oldCurrent != null) await ArchiveSupersededAsync(ctx, oldCurrent, existing);
            }
            ctx.FilesPromoted++;
        }

        private async Task<StoredFile> CopyBlobAsync(FileVersion version, Folder targetFolder)
        {
            using var stream = await _storage.OpenReadAsync(version.StoragePath);
            var ext = "." + version.Format;
            return await _storage.SaveAsync(stream, targetFolder.ProjectId, targetFolder.Id, ext);
        }

        private async Task ArchiveSupersededAsync(Ctx ctx, FileVersion oldVersion, FileItem targetItem)
        {
            var archived = ResolveArchivedFolder(ctx, targetItem.FolderId);
            if (archived == null) { oldVersion.IsHidden = true; return; }

            var now = DateTime.UtcNow;
            var archItem = new FileItem
            {
                Id = Guid.NewGuid(),
                FolderId = archived.Id,
                Name = $"{targetItem.Name} (v{oldVersion.VersionNumber})",
                FileType = targetItem.FileType,
                CurrentVersionId = oldVersion.Id,
                CreatedByAccountId = ctx.Actor,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _unitOfWork.Repository<FileItem>().CreateAsync(archItem);
            ctx.Files.Add(archItem);
            oldVersion.FileItemId = archItem.Id;   // chuyển bản cũ sang mục Archived
        }

        // ---------- move (Published → Archived) ----------

        private async Task MoveFolderRecursiveAsync(Ctx ctx, Folder source, Folder target, CdeArea targetArea)
        {
            foreach (var file in FilesInFolder(ctx, source.Id).ToList())
                MoveFileInto(ctx, file, target);
            foreach (var childSrc in ChildrenOf(ctx, source.Id).Where(c => !c.IsTemplate).ToList())
            {
                var childTarget = await EnsureMirrorChildAsync(ctx, target, childSrc, targetArea);
                await MoveFolderRecursiveAsync(ctx, childSrc, childTarget, targetArea);
            }
        }

        private void MoveFileInto(Ctx ctx, FileItem sourceFile, Folder targetFolder)
        {
            var now = DateTime.UtcNow;
            var clash = FilesInFolder(ctx, targetFolder.Id)
                .Any(f => f.Id != sourceFile.Id
                       && string.Equals(f.Name, sourceFile.Name, StringComparison.OrdinalIgnoreCase));
            if (clash) sourceFile.Name = $"{sourceFile.Name} ({now:yyyyMMddHHmmss})";
            sourceFile.FolderId = targetFolder.Id;   // re-parent (blob giữ nguyên)
            sourceFile.UpdatedAt = now;
            ctx.FilesPromoted++;
        }

        // ---------- helpers ----------

        private IEnumerable<FileItem> FilesInFolder(Ctx ctx, Guid folderId)
            => ctx.Files.Where(f => f.FolderId == folderId);

        private IEnumerable<FileVersion> VersionsOf(Ctx ctx, Guid fileItemId)
            => ctx.Versions.Where(v => v.FileItemId == fileItemId);

        private FileVersion? CurrentVersionOf(Ctx ctx, FileItem file)
            => VersionsOf(ctx, file.Id).FirstOrDefault(v => v.Id == file.CurrentVersionId)
               ?? VersionsOf(ctx, file.Id).OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        private Folder? ResolveArchivedFolder(Ctx ctx, Guid folderId)
        {
            var folder = ctx.Folders.First(f => f.Id == folderId);
            Guid? ownerGroupId = folder.OwnerGroupId;
            var cur = folder;
            while (!ownerGroupId.HasValue && cur.ParentFolderId.HasValue)
            {
                var parent = ctx.Folders.FirstOrDefault(f => f.Id == cur.ParentFolderId.Value);
                if (parent == null) break;
                ownerGroupId = parent.OwnerGroupId;
                cur = parent;
            }

            var archivedRoot = ctx.Folders.FirstOrDefault(f => f.ParentFolderId == null && f.Area == CdeArea.Archived);
            if (archivedRoot == null) return null;

            if (ownerGroupId.HasValue)
            {
                var groupArch = ctx.Folders.FirstOrDefault(
                    f => f.ParentFolderId == archivedRoot.Id && f.OwnerGroupId == ownerGroupId.Value);
                if (groupArch != null) return groupArch;
            }
            return archivedRoot;
        }

        private static FileVersion NewVersionFrom(
            FileVersion source, Guid fileItemId, int number, StoredFile stored, Guid actor, DateTime now) => new()
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItemId,
                VersionNumber = number,
                StoragePath = stored.RelativePath,
                FileSizeBytes = stored.SizeBytes,
                Format = source.Format,
                Checksum = stored.Checksum,
                IsHidden = false,
                UploadedByAccountId = actor,
                UploadedAt = now,
                SourceFileVersionId = source.Id,
            };

        private static TransitionResultDTO Result(Folder target, CdeArea targetArea, Ctx ctx) => new()
        {
            TargetFolderId = target.Id,
            TargetArea = targetArea,
            FoldersCreated = ctx.FoldersCreated,
            FilesPromoted = ctx.FilesPromoted,
            Moved = IsMove(targetArea),
        };
    }
}
