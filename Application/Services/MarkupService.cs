using Application.DTOs.RequestDTOs.Markup;
using Application.DTOs.ResponseDTOs.Markup;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.File;

namespace Application.Services
{
    public class MarkupService : IMarkupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderTreeRepository _folderTree;
        private readonly IPermissionCheckingRepository _permissionRepo;
        private readonly IMarkupBroadcaster _broadcaster;
        private readonly INotificationService _notification;

        public MarkupService(
            IUnitOfWork unitOfWork,
            IFolderTreeRepository folderTree,
            IPermissionCheckingRepository permissionRepo,
            IMarkupBroadcaster broadcaster,
            INotificationService notification)
        {
            _unitOfWork = unitOfWork;
            _folderTree = folderTree;
            _permissionRepo = permissionRepo;
            _broadcaster = broadcaster;
            _notification = notification;
        }


        public async Task<MarkupSetResponseDTO> CreateSetAsync(CreateMarkupSetDTO dto, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await GetFileItemAsync(dto.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId, isSystemAdmin);

            var versionId = dto.FileVersionId ?? fileItem.CurrentVersionId
                ?? throw new ApiExceptionResponse("File has no content version to markup.", 400);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(versionId)
                ?? throw new ApiExceptionResponse("File version not found.", 404);
            if (version.FileItemId != fileItem.Id)
                throw new ApiExceptionResponse("Version does not belong to this file.", 400);

            var now = DateTime.UtcNow;
            var set = new MarkupSet
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItem.Id,
                FileVersionId = version.Id,
                Title = dto.Title,
                Status = MarkupSetStatus.Open,
                IssueId = dto.IssueId,
                CreatedByAccountId = actorId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _unitOfWork.Repository<MarkupSet>().CreateAsync(set);
            await _unitOfWork.CommitAsync();

            var actorName = await GetAccountNameAsync(actorId);
            return BuildSetDto(set, version.WorkingVersion, actorName, 0, 0, new List<FileNoteResponseDTO>());
        }

        public async Task<IEnumerable<MarkupSetResponseDTO>> GetSetsByFileAsync(Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await GetFileItemAsync(fileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId, isSystemAdmin);

            var sets = (await _unitOfWork.Repository<MarkupSet>().FindAsync(s => s.FileItemId == fileItemId))
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            return await BuildSetSummariesAsync(sets);
        }

        public async Task<IEnumerable<MarkupSetResponseDTO>> GetSetsByIssueAsync(
            Guid issueId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var sets = (await _unitOfWork.Repository<MarkupSet>()
                    .FindAsync(s => s.IssueId == issueId))
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
            if (sets.Count == 0) return Enumerable.Empty<MarkupSetResponseDTO>();

            var fileItemIds = sets.Select(s => s.FileItemId).Distinct().ToList();
            var fileItems = (await _unitOfWork.Repository<FileItem>().FindAsync(f => fileItemIds.Contains(f.Id)))
                .ToDictionary(f => f.Id);

            var canViewFolder = new Dictionary<Guid, bool>();
            var visible = new List<MarkupSet>();
            foreach (var set in sets)
            {
                if (!fileItems.TryGetValue(set.FileItemId, out var fi)) continue;
                if (!canViewFolder.TryGetValue(fi.FolderId, out var allowed))
                {
                    var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fi.FolderId);
                    allowed = folder is not null && await CanAccessFolderAsync(folder, actorId, isSystemAdmin);
                    canViewFolder[fi.FolderId] = allowed;
                }
                if (allowed) visible.Add(set);
            }

            return await BuildSetSummariesAsync(visible);
        }

        public async Task<MarkupSetResponseDTO> GetSetDetailAsync(Guid setId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId, isSystemAdmin);

            var notes = (await _unitOfWork.Repository<FileNote>().FindAsync(n => n.MarkupSetId == set.Id))
                .OrderBy(n => n.CreatedAt)
                .ToList();

            var accounts = await LoadAccountNamesAsync(
                notes.Select(n => n.AuthorAccountId).Append(set.CreatedByAccountId));
            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(set.FileVersionId);

            var noteDtos = notes.Select(n => BuildNoteDto(n, NameOf(accounts, n.AuthorAccountId))).ToList();
            return BuildSetDto(
                set,
                version?.WorkingVersion ?? 0,
                NameOf(accounts, set.CreatedByAccountId),
                noteDtos.Count,
                noteDtos.Count(d => d.Status == FileNoteStatus.Open),
                noteDtos);
        }

        public async Task<MarkupSetResponseDTO> UpdateSetStatusAsync(Guid setId, MarkupSetStatus status, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId, isSystemAdmin);

            set.Status = status;
            set.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<MarkupSet>().Update(set);
            await _unitOfWork.CommitAsync();

            return await BuildSetDetailDtoAsync(set);
        }

        public async Task<MarkupSetResponseDTO> LinkToIssueAsync(
            Guid setId, Guid? issueId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId, isSystemAdmin);

            set.IssueId = issueId;
            set.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<MarkupSet>().Update(set);
            await _unitOfWork.CommitAsync();

            return await BuildSetDetailDtoAsync(set);
        }

        public async Task<FileNoteResponseDTO> AddNoteAsync(Guid setId, CreateFileNoteDTO dto, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId, isSystemAdmin);

            var now = DateTime.UtcNow;
            var note = new FileNote
            {
                Id = Guid.NewGuid(),
                MarkupSetId = set.Id,
                FileVersionId = set.FileVersionId,
                PageNumber = dto.PageNumber,
                MarkupType = dto.MarkupType,
                CoordinateJson = string.IsNullOrWhiteSpace(dto.CoordinateJson) ? "{}" : dto.CoordinateJson,
                StyleJson = dto.StyleJson,
                Content = dto.Content,
                ViewpointStateJson = dto.ViewpointStateJson,
                MarkupSvg = dto.MarkupSvg,
                ThumbnailDataUrl = dto.ThumbnailDataUrl,
                Status = FileNoteStatus.Open,
                AuthorAccountId = actorId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await _unitOfWork.Repository<FileNote>().CreateAsync(note);

            set.UpdatedAt = now;
            await _unitOfWork.CommitAsync();

            var actorName = await GetAccountNameAsync(actorId);
            var result = BuildNoteDto(note, actorName);

            await _broadcaster.NoteAddedAsync(fileItem.Id, result);
            await NotifySetFollowersAsync(set, actorId, actorName, fileItem.Name);
            return result;
        }

        public async Task<FileNoteResponseDTO> UpdateNoteAsync(Guid noteId, UpdateFileNoteDTO dto, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var note = await GetNoteAsync(noteId);
            var set = await GetSetAsync(note.MarkupSetId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanMutateNoteAsync(actorId, note, fileItem.FolderId, isSystemAdmin);

            if (dto.MarkupType.HasValue) note.MarkupType = dto.MarkupType.Value;
            if (dto.PageNumber.HasValue) note.PageNumber = dto.PageNumber;
            if (dto.CoordinateJson is not null) note.CoordinateJson = dto.CoordinateJson;
            if (dto.StyleJson is not null) note.StyleJson = dto.StyleJson;
            if (dto.Content is not null) note.Content = dto.Content;
            if (dto.Status.HasValue) note.Status = dto.Status.Value;
            note.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<FileNote>().Update(note);
            await _unitOfWork.CommitAsync();

            var authorName = await GetAccountNameAsync(note.AuthorAccountId);
            var result = BuildNoteDto(note, authorName);
            await _broadcaster.NoteUpdatedAsync(fileItem.Id, result);
            return result;
        }

        public async Task DeleteNoteAsync(Guid noteId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var note = await GetNoteAsync(noteId);
            var set = await GetSetAsync(note.MarkupSetId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanMutateNoteAsync(actorId, note, fileItem.FolderId, isSystemAdmin);

            _unitOfWork.Repository<FileNote>().Delete(note);
            await _unitOfWork.CommitAsync();

            await _broadcaster.NoteDeletedAsync(fileItem.Id, noteId);
        }

        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        private async Task<MarkupSet> GetSetAsync(Guid setId)
            => await _unitOfWork.Repository<MarkupSet>().GetByIdAsync(setId)
               ?? throw new ApiExceptionResponse("Markup set not found.", 404);

        private async Task<FileNote> GetNoteAsync(Guid noteId)
            => await _unitOfWork.Repository<FileNote>().GetByIdAsync(noteId)
               ?? throw new ApiExceptionResponse("Markup note not found.", 404);

        public async Task<bool> CanAccessFileMarkupAsync(Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (fileItem is null) return false;

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);
            return folder is not null && await CanAccessFolderAsync(folder, actorId, isSystemAdmin);
        }

        private async Task<bool> CanAccessFolderAsync(Folder folder, Guid actorId, bool isSystemAdmin)
            => isSystemAdmin
               || await _folderTree.HasFullAccessAsync(folder.ProjectId, actorId)
               || (await _permissionRepo.GetUserFolderPermissionAsync(folder.Id, actorId)) is { CanView: true };

        private async Task RequireCanAccessFileAsync(FileItem fileItem, Guid actorId, bool isSystemAdmin)
        {
            var folder = await GetFolderAsync(fileItem.FolderId);
            if (!await CanAccessFolderAsync(folder, actorId, isSystemAdmin))
                throw new ApiExceptionResponse("Bạn không có quyền xem markup của file này.", 403);
        }

        private async Task RequireCanMutateNoteAsync(Guid actorId, FileNote note, Guid folderId, bool isSystemAdmin)
        {
            if (note.AuthorAccountId == actorId) return;

            var folder = await GetFolderAsync(folderId);
            if (isSystemAdmin || await _folderTree.HasFullAccessAsync(folder.ProjectId, actorId)) return;

            var permission = await _permissionRepo.GetUserFolderPermissionAsync(folderId, actorId);
            if (permission is not { CanEdit: true })
                throw new ApiExceptionResponse("Bạn cần quyền Sửa trên thư mục này để sửa/xóa ghi chú của người khác.", 403);
        }

        private async Task<Folder> GetFolderAsync(Guid folderId)
            => await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
               ?? throw new ApiExceptionResponse("File folder not found.", 404);

        private async Task NotifySetFollowersAsync(MarkupSet set, Guid actorId, string? actorName, string fileName)
        {
            var noteAuthors = (await _unitOfWork.Repository<FileNote>().FindAsync(n => n.MarkupSetId == set.Id))
                .Where(n => n.AuthorAccountId.HasValue)
                .Select(n => n.AuthorAccountId!.Value);

            var followers = noteAuthors
                .Append(set.CreatedByAccountId ?? Guid.Empty)
                .Where(id => id != Guid.Empty && id != actorId)
                .Distinct()
                .ToList();
            if (followers.Count == 0) return;

            await _notification.NotifyManyAsync(
                followers,
                $"{actorName ?? "Một người dùng"} vừa thêm ghi chú markup trên file \"{fileName}\".",
                senderName: actorName,
                linkType: "Markup",
                linkId: set.Id.ToString());
        }

        private async Task<MarkupSetResponseDTO> BuildSetDetailDtoAsync(MarkupSet set)
        {
            var notes = (await _unitOfWork.Repository<FileNote>().FindAsync(n => n.MarkupSetId == set.Id)).ToList();
            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(set.FileVersionId);
            var createdByName = await GetAccountNameAsync(set.CreatedByAccountId);
            return BuildSetDto(
                set, version?.WorkingVersion ?? 0, createdByName,
                notes.Count, notes.Count(n => n.Status == FileNoteStatus.Open), new List<FileNoteResponseDTO>());
        }

        private async Task<List<MarkupSetResponseDTO>> BuildSetSummariesAsync(List<MarkupSet> sets)
        {
            if (sets.Count == 0) return new List<MarkupSetResponseDTO>();

            var setIds = sets.Select(s => s.Id).ToList();
            var notesBySet = (await _unitOfWork.Repository<FileNote>().FindAsync(n => setIds.Contains(n.MarkupSetId)))
                .GroupBy(n => n.MarkupSetId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var versionIds = sets.Select(s => s.FileVersionId).Distinct().ToList();
            var versions = (await _unitOfWork.Repository<FileVersionState>().FindAsync(v => versionIds.Contains(v.Id)))
                .ToDictionary(v => v.Id, v => v.WorkingVersion);

            var accounts = await LoadAccountNamesAsync(sets.Select(s => s.CreatedByAccountId));

            return sets.Select(set =>
            {
                notesBySet.TryGetValue(set.Id, out var notes);
                var count = notes?.Count ?? 0;
                var open = notes?.Count(n => n.Status == FileNoteStatus.Open) ?? 0;
                versions.TryGetValue(set.FileVersionId, out var versionNumber);
                return BuildSetDto(set, versionNumber, NameOf(accounts, set.CreatedByAccountId), count, open, new List<FileNoteResponseDTO>());
            }).ToList();
        }

        private static MarkupSetResponseDTO BuildSetDto(
            MarkupSet set, int versionNumber, string? createdByName, int noteCount, int openNoteCount, List<FileNoteResponseDTO> notes)
            => new()
            {
                Id = set.Id,
                FileItemId = set.FileItemId,
                FileVersionId = set.FileVersionId,
                VersionNumber = versionNumber,
                Title = set.Title,
                Status = set.Status,
                IssueId = set.IssueId,
                SnapshotStoragePath = set.SnapshotStoragePath,
                CreatedByAccountId = set.CreatedByAccountId,
                CreatedByName = createdByName,
                CreatedAt = set.CreatedAt,
                UpdatedAt = set.UpdatedAt,
                NoteCount = noteCount,
                OpenNoteCount = openNoteCount,
                Notes = notes,
            };

        private static FileNoteResponseDTO BuildNoteDto(FileNote n, string? authorName)
            => new()
            {
                Id = n.Id,
                MarkupSetId = n.MarkupSetId,
                FileVersionId = n.FileVersionId,
                PageNumber = n.PageNumber,
                MarkupType = n.MarkupType,
                CoordinateJson = n.CoordinateJson,
                StyleJson = n.StyleJson,
                Content = n.Content,
                ViewpointStateJson = n.ViewpointStateJson,
                MarkupSvg = n.MarkupSvg,
                ThumbnailDataUrl = n.ThumbnailDataUrl,
                Status = n.Status,
                AuthorAccountId = n.AuthorAccountId,
                AuthorName = authorName,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
            };

        private async Task<string?> GetAccountNameAsync(Guid? accountId)
        {
            if (!accountId.HasValue) return null;
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId.Value);
            return account?.UserName;
        }

        private async Task<Dictionary<Guid, string>> LoadAccountNamesAsync(IEnumerable<Guid?> ids)
        {
            var idSet = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            if (idSet.Count == 0) return new Dictionary<Guid, string>();

            return (await _unitOfWork.Repository<Account>().FindAsync(a => idSet.Contains(a.Id)))
                .ToDictionary(a => a.Id, a => a.UserName);
        }

        private static string? NameOf(IReadOnlyDictionary<Guid, string> accounts, Guid? id)
            => id.HasValue && accounts.TryGetValue(id.Value, out var name) ? name : null;
    }
}
