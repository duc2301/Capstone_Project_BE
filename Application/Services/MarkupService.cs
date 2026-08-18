using Application.DTOs.RequestDTOs.Markup;
using Application.DTOs.ResponseDTOs.Markup;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.File;

namespace Application.Services
{
    public class MarkupService : IMarkupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMarkupRepository _markups;
        private readonly IPermissionCheckingService _permission;
        private readonly IMarkupBroadcaster _broadcaster;
        private readonly INotificationService _notification;
        private readonly IAuditLogService _auditLog;
        private readonly IIssueActivityService _issueActivity;

        public MarkupService(
            IUnitOfWork unitOfWork,
            IMarkupRepository markups,
            IPermissionCheckingService permission,
            IMarkupBroadcaster broadcaster,
            INotificationService notification,
            IAuditLogService auditLog,
            IIssueActivityService issueActivity)
        {
            _unitOfWork = unitOfWork;
            _markups = markups;
            _permission = permission;
            _broadcaster = broadcaster;
            _notification = notification;
            _auditLog = auditLog;
            _issueActivity = issueActivity;
        }


        public async Task<MarkupSetResponseDTO> CreateSetAsync(CreateMarkupSetDTO dto, Guid actorId, CancellationToken ct = default)
        {
            var fileItem = await GetFileItemAsync(dto.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId);

            var versionId = dto.FileVersionId ?? fileItem.CurrentVersionId
                ?? throw new ApiExceptionResponse("File has no content version to markup.", 400);

            var version = await _markups.GetVersionAsync(versionId, ct)
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
            await LogMarkupAsync(AuditAction.Create, set, fileItem, actorId, $"Tạo bộ ghi chú '{set.Title}' trên '{fileItem.Name}'");
            await _unitOfWork.CommitAsync();

            if (set.IssueId.HasValue)
                await _issueActivity.MarkInProgressOnActivityAsync(set.IssueId.Value, actorId);

            var actorName = await GetAccountNameAsync(actorId);
            return BuildSetDto(set, version.WorkingVersion, actorName, 0, 0, new List<FileNoteResponseDTO>());
        }

        public async Task<IEnumerable<MarkupSetResponseDTO>> GetSetsByFileAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default)
        {
            var fileItem = await GetFileItemAsync(fileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId);

            var sets = await _markups.GetSetsByFileAsync(fileItemId, ct);

            return await BuildSetSummariesAsync(sets, ct);
        }

        public async Task<IEnumerable<MarkupSetResponseDTO>> GetSetsByIssueAsync(
            Guid issueId, Guid actorId, CancellationToken ct = default)
        {
            var sets = await _markups.GetSetsByIssueAsync(issueId, ct);
            if (sets.Count == 0) return Enumerable.Empty<MarkupSetResponseDTO>();

            var fileItems = await _markups.GetFileItemsAsync(sets.Select(s => s.FileItemId), ct);

            var canViewFile = new Dictionary<Guid, bool>();
            var visible = new List<MarkupSet>();
            foreach (var set in sets)
            {
                if (!fileItems.TryGetValue(set.FileItemId, out var fi)) continue;
                if (!canViewFile.TryGetValue(fi.Id, out var allowed))
                {
                    allowed = await _permission.HasViewFileAsync(fi.Id, actorId);
                    canViewFile[fi.Id] = allowed;
                }
                if (allowed) visible.Add(set);
            }

            return await BuildSetSummariesAsync(visible, ct);
        }

        public async Task<MarkupSetResponseDTO> GetSetDetailAsync(Guid setId, Guid actorId, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId);

            var notes = await _markups.GetNotesBySetAsync(set.Id, ct);

            var accounts = await LoadAccountNamesAsync(
                notes.Select(n => n.AuthorAccountId).Append(set.CreatedByAccountId), ct);
            var version = await _markups.GetVersionAsync(set.FileVersionId, ct);

            var noteDtos = notes.Select(n => BuildNoteDto(n, NameOf(accounts, n.AuthorAccountId))).ToList();
            return BuildSetDto(
                set,
                version?.WorkingVersion ?? 0,
                NameOf(accounts, set.CreatedByAccountId),
                noteDtos.Count,
                noteDtos.Count(d => d.Status == FileNoteStatus.Open),
                noteDtos);
        }

        public async Task<MarkupSetResponseDTO> UpdateSetStatusAsync(Guid setId, MarkupSetStatus status, Guid actorId, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId);

            set.Status = status;
            set.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<MarkupSet>().Update(set);
            await LogMarkupAsync(AuditAction.StatusChange, set, fileItem, actorId, $"Bộ ghi chú '{set.Title}' chuyển sang {status}");
            await _unitOfWork.CommitAsync();

            return await BuildSetDetailDtoAsync(set);
        }

        private async Task LogMarkupAsync(
            AuditAction action, MarkupSet set, FileItem fileItem, Guid actorId, string detail)
        {
            var projectId = await _markups.GetProjectIdByFolderAsync(fileItem.FolderId);
            await _auditLog.LogAsync(
                LogScope.Group, action, nameof(MarkupSet), set.Id.ToString(), actorId,
                detail: detail,
                projectId: projectId, folderId: fileItem.FolderId);
        }

        public async Task<MarkupSetResponseDTO> LinkToIssueAsync(
            Guid setId, Guid? issueId, Guid actorId, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId);

            set.IssueId = issueId;
            set.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<MarkupSet>().Update(set);
            await _unitOfWork.CommitAsync();

            return await BuildSetDetailDtoAsync(set);
        }

        public async Task<FileNoteResponseDTO> AddNoteAsync(Guid setId, CreateFileNoteDTO dto, Guid actorId, CancellationToken ct = default)
        {
            var set = await GetSetAsync(setId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanAccessFileAsync(fileItem, actorId);

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

        public async Task<FileNoteResponseDTO> UpdateNoteAsync(Guid noteId, UpdateFileNoteDTO dto, Guid actorId, CancellationToken ct = default)
        {
            var note = await GetNoteAsync(noteId);
            var set = await GetSetAsync(note.MarkupSetId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanMutateNoteAsync(actorId, note, fileItem.FolderId);

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

        public async Task DeleteNoteAsync(Guid noteId, Guid actorId, CancellationToken ct = default)
        {
            var note = await GetNoteAsync(noteId);
            var set = await GetSetAsync(note.MarkupSetId);
            var fileItem = await GetFileItemAsync(set.FileItemId);
            await RequireCanMutateNoteAsync(actorId, note, fileItem.FolderId);

            _unitOfWork.Repository<FileNote>().Delete(note);
            await _unitOfWork.CommitAsync();

            await _broadcaster.NoteDeletedAsync(fileItem.Id, noteId);
        }

        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _markups.GetFileItemAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        private async Task<MarkupSet> GetSetAsync(Guid setId)
            => await _markups.GetSetForUpdateAsync(setId)
               ?? throw new ApiExceptionResponse("Markup set not found.", 404);

        private async Task<FileNote> GetNoteAsync(Guid noteId)
            => await _markups.GetNoteForUpdateAsync(noteId)
               ?? throw new ApiExceptionResponse("Markup note not found.", 404);

        // View markup = View the file (file-level override, else folder ACL). PM/system-admin bypass
        // handled inside PermissionCheckingService.
        public Task<bool> CanAccessFileMarkupAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default)
            => _permission.HasViewFileAsync(fileItemId, actorId);

        private async Task RequireCanAccessFileAsync(FileItem fileItem, Guid actorId)
        {
            if (!await _permission.HasViewFileAsync(fileItem.Id, actorId))
                throw new ApiExceptionResponse("Bạn không có quyền xem markup của file này.", 403);
        }

        // Sửa/xóa ghi chú của người khác cần quyền Sửa trên thư mục (tác giả luôn tự sửa được).
        private async Task RequireCanMutateNoteAsync(Guid actorId, FileNote note, Guid folderId)
        {
            if (note.AuthorAccountId == actorId) return;

            if (!await _permission.HasEditFolderAsync(folderId, actorId))
                throw new ApiExceptionResponse("Bạn cần quyền Sửa trên thư mục này để sửa/xóa ghi chú của người khác.", 403);
        }

        private async Task NotifySetFollowersAsync(MarkupSet set, Guid actorId, string? actorName, string fileName)
        {
            var noteAuthors = await _markups.GetNoteAuthorIdsBySetAsync(set.Id);

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
            var counts = await _markups.GetNoteCountsBySetAsync(set.Id);
            var version = await _markups.GetVersionAsync(set.FileVersionId);
            var createdByName = await GetAccountNameAsync(set.CreatedByAccountId);
            return BuildSetDto(
                set, version?.WorkingVersion ?? 0, createdByName,
                counts.Total, counts.Open, new List<FileNoteResponseDTO>());
        }

        private async Task<List<MarkupSetResponseDTO>> BuildSetSummariesAsync(
            IReadOnlyList<MarkupSet> sets, CancellationToken ct = default)
        {
            if (sets.Count == 0) return new List<MarkupSetResponseDTO>();

            var countsBySet = await _markups.GetNoteCountsBySetsAsync(sets.Select(s => s.Id), ct);
            var versions = await _markups.GetVersionNumbersAsync(sets.Select(s => s.FileVersionId), ct);
            var accounts = await LoadAccountNamesAsync(sets.Select(s => s.CreatedByAccountId), ct);

            return sets.Select(set =>
            {
                countsBySet.TryGetValue(set.Id, out var counts);
                versions.TryGetValue(set.FileVersionId, out var versionNumber);
                return BuildSetDto(
                    set, versionNumber, NameOf(accounts, set.CreatedByAccountId),
                    counts?.Total ?? 0, counts?.Open ?? 0, new List<FileNoteResponseDTO>());
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
            return await _markups.GetAccountNameAsync(accountId.Value);
        }

        private async Task<IReadOnlyDictionary<Guid, string>> LoadAccountNamesAsync(
            IEnumerable<Guid?> ids, CancellationToken ct = default)
        {
            var idSet = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            if (idSet.Count == 0) return new Dictionary<Guid, string>();

            return await _markups.GetAccountNamesAsync(idSet, ct);
        }

        private static string? NameOf(IReadOnlyDictionary<Guid, string> accounts, Guid? id)
            => id.HasValue && accounts.TryGetValue(id.Value, out var name) ? name : null;
    }
}
