using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Application.Services
{
    public class FileDeletionService : IFileDeletionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileDeletionRepository _files;
        private readonly IFileStorageService _storage;
        private readonly IAuditLogService _auditLog;

        public FileDeletionService(
            IUnitOfWork unitOfWork,
            IFileDeletionRepository files,
            IFileStorageService storage,
            IAuditLogService auditLog)
        {
            _unitOfWork = unitOfWork;
            _files = files;
            _storage = storage;
            _auditLog = auditLog;
        }

        public async Task<DeleteFileResultDTO> DeleteFlaggedAsync(
            Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await _files.GetFileItemForUpdateAsync(fileItemId, ct)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var folder = await _files.GetFolderAsync(fileItem.FolderId, ct)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            if (folder.Area != CdeArea.Wip)
                throw new ApiExceptionResponse("Chỉ xoá được tệp đang ở vùng WIP.", 400);
            if (fileItem.Status != FileItemStatus.Draft)
                throw new ApiExceptionResponse("Tệp đã gửi duyệt nên không xoá được.", 400);
            if (fileItem.IsSigned)
                throw new ApiExceptionResponse("Tệp đã ký số nên không xoá được.", 400);

            var current = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File has no content version.", 404);

            if (current.Warnning != true)
                throw new ApiExceptionResponse("Chỉ xoá được phiên bản bị AI cảnh báo nội dung.", 400);

            await RequireDeletePermissionAsync(folder.ProjectId, current, actorId, isSystemAdmin, ct);
            await RequireNoOpenIssueAsync(fileItemId, ct);
            await RequireNoPendingApprovalAsync(fileItemId, ct);
            await RequireNoReturnRequestAsync(fileItemId, ct);

            var history = await _unitOfWork.FileVersionRepository.GetHistoryAsync(fileItemId);
            var previous = history
                .Where(v => v.Id != current.Id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefault();

            var deletedVersion = current.DisplayVersion;
            var storagePaths = new List<string>();
            CollectStoragePath(storagePaths, current);

            await DeleteVersionDependenciesAsync(current.Id, ct);

            if (previous == null)
            {
                await RequireNoApprovalHistoryAsync(fileItemId, ct);
                await DeleteFileItemDependenciesAsync(fileItem, ct);
                _unitOfWork.Repository<FileVersionState>().Delete(current);

                fileItem.CurrentVersionId = null;
                _unitOfWork.Repository<FileItem>().Delete(fileItem);

                await _auditLog.LogAsync(
                    LogScope.Group, AuditAction.Delete, nameof(FileItem), fileItem.Id.ToString(), actorId,
                    detail: $"Xoá tệp '{fileItem.Name}' ({deletedVersion}) do AI cảnh báo nội dung",
                    projectId: folder.ProjectId, folderId: folder.Id);
                await _unitOfWork.CommitAsync();

                await DeleteFromStorageAsync(storagePaths, ct);

                return new DeleteFileResultDTO
                {
                    FileItemId = fileItemId,
                    FileRemoved = true,
                    DeletedVersion = deletedVersion,
                    CurrentVersion = null
                };
            }

            var restored = await _files.GetVersionForUpdateAsync(previous.Id, ct)
                ?? throw new ApiExceptionResponse("Previous version not found.", 404);

            _unitOfWork.Repository<FileVersionState>().Delete(current);
            await _unitOfWork.CommitAsync();

            restored.IsCurrent = true;
            restored.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<FileVersionState>().Update(restored);

            fileItem.CurrentVersionId = restored.Id;
            fileItem.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<FileItem>().Update(fileItem);

            await _auditLog.LogAsync(
                LogScope.Group, AuditAction.Delete, nameof(FileVersionState), current.Id.ToString(), actorId,
                detail: $"Xoá phiên bản {deletedVersion} của '{fileItem.Name}' do AI cảnh báo nội dung, "
                      + $"khôi phục {restored.DisplayVersion} làm bản hiện hành",
                projectId: folder.ProjectId, folderId: folder.Id);
            await _unitOfWork.CommitAsync();

            await DeleteFromStorageAsync(storagePaths, ct);

            return new DeleteFileResultDTO
            {
                FileItemId = fileItemId,
                FileRemoved = false,
                DeletedVersion = deletedVersion,
                CurrentVersion = restored.DisplayVersion
            };
        }

        private async Task RequireDeletePermissionAsync(
            Guid projectId, FileVersionState version, Guid actorId, bool isSystemAdmin, CancellationToken ct)
        {
            if (isSystemAdmin) return;
            if (version.UploadedByAccountId == actorId) return;

            var managerAccountId = await _files.GetProjectManagerIdAsync(projectId, ct);
            if (managerAccountId == actorId) return;

            throw new ApiExceptionResponse(
                "Chỉ người tải lên, quản lý dự án hoặc quản trị hệ thống được xoá tệp này.", 403);
        }

        private async Task RequireNoOpenIssueAsync(Guid fileItemId, CancellationToken ct)
        {
            var openIssues = await _files.CountOpenIssuesAsync(fileItemId, ct);
            if (openIssues > 0)
                throw new ApiExceptionResponse(
                    $"Tệp còn {openIssues} vấn đề chưa đóng. Đóng hết rồi mới xoá được.", 409);
        }

        private async Task RequireNoPendingApprovalAsync(Guid fileItemId, CancellationToken ct)
        {
            if (await _files.HasPendingApprovalAsync(fileItemId, ct))
                throw new ApiExceptionResponse("Tệp đang có phiếu duyệt chờ xử lý nên không xoá được.", 409);
        }

        private async Task RequireNoApprovalHistoryAsync(Guid fileItemId, CancellationToken ct)
        {
            if (await _files.HasAnyApprovalAsync(fileItemId, ct))
                throw new ApiExceptionResponse(
                    "Tệp đã từng qua phiếu duyệt nên không xoá khỏi hệ thống được.", 409);
        }

        private async Task RequireNoReturnRequestAsync(Guid fileItemId, CancellationToken ct)
        {
            if (await _files.HasReturnRequestAsync(fileItemId, ct))
                throw new ApiExceptionResponse(
                    "Tệp có yêu cầu trả về vùng WIP nên không xoá được.", 409);
        }

        private async Task DeleteVersionDependenciesAsync(Guid versionId, CancellationToken ct)
        {
            var loiChecks = await _files.GetLoiChecksForDeleteAsync(versionId, ct);
            foreach (var check in loiChecks)
                _unitOfWork.Repository<FileVersionLoiCheck>().Delete(check);

            var markupSets = await _files.GetMarkupSetsForDeleteAsync(versionId, ct);
            var setIds = markupSets.Select(s => s.Id).ToList();

            var notes = await _files.GetNotesForDeleteAsync(versionId, setIds, ct);
            foreach (var note in notes)
                _unitOfWork.Repository<FileNote>().Delete(note);

            foreach (var set in markupSets)
                _unitOfWork.Repository<MarkupSet>().Delete(set);
        }

        private async Task DeleteFileItemDependenciesAsync(FileItem fileItem, CancellationToken ct)
        {
            var linkedIssues = await _files.GetLinkedIssuesForUpdateAsync(fileItem.Id, ct);
            foreach (var issue in linkedIssues)
            {
                issue.LinkedFileItemId = null;
                _unitOfWork.Repository<Issue>().Update(issue);
            }

            var links = await _files.GetLinksForDeleteAsync(fileItem.Id, ct);
            foreach (var link in links)
                _unitOfWork.Repository<FileLink>().Delete(link);

            var permissions = await _files.GetFilePermissionsForDeleteAsync(fileItem.Id, ct);
            foreach (var permission in permissions)
                _unitOfWork.Repository<FilePermission>().Delete(permission);

            var namingMetadata = await _files.GetNamingMetadataForDeleteAsync(fileItem.Id, ct);
            foreach (var metadata in namingMetadata)
                _unitOfWork.Repository<FileNamingMetadata>().Delete(metadata);

            var signaturePositions = await _files.GetSignaturePositionsForDeleteAsync(fileItem.Id, ct);
            foreach (var position in signaturePositions)
                _unitOfWork.Repository<FileSignaturePosition>().Delete(position);

            var viewGrants = await _files.GetViewGrantsForDeleteAsync(fileItem.Id, ct);
            foreach (var grant in viewGrants)
                _unitOfWork.Repository<FileViewGrant>().Delete(grant);

            var documents = await _files.GetDocumentsForDeleteAsync(fileItem.Id, ct);
            foreach (var document in documents)
                _unitOfWork.Repository<Document>().Delete(document);
        }

        private static void CollectStoragePath(List<string> paths, FileVersionState version)
        {
            if (!string.IsNullOrWhiteSpace(version.StoragePath)) paths.Add(version.StoragePath);
            if (!string.IsNullOrWhiteSpace(version.PreviewStoragePath)) paths.Add(version.PreviewStoragePath);
        }

        private async Task DeleteFromStorageAsync(IEnumerable<string> storagePaths, CancellationToken ct)
        {
            foreach (var path in storagePaths.Distinct())
                await _storage.DeleteAsync(path, ct);
        }
    }
}
