using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Issue;

namespace Application.Services
{
    public class FileDeletionService : IFileDeletionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IAuditLogService _auditLog;

        public FileDeletionService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            IAuditLogService auditLog)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _auditLog = auditLog;
        }

        public async Task<DeleteFileResultDTO> DeleteFlaggedAsync(
            Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
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

            await RequireDeletePermissionAsync(folder.ProjectId, current, actorId, isSystemAdmin);
            await RequireNoOpenIssueAsync(fileItemId);
            await RequireNoPendingApprovalAsync(fileItemId);
            await RequireNoReturnRequestAsync(fileItemId);

            var history = await _unitOfWork.FileVersionRepository.GetHistoryAsync(fileItemId);
            var previous = history
                .Where(v => v.Id != current.Id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefault();

            var deletedVersion = current.DisplayVersion;
            var storagePaths = new List<string>();
            CollectStoragePath(storagePaths, current);

            await DeleteVersionDependenciesAsync(current.Id);

            if (previous == null)
            {
                await RequireNoApprovalHistoryAsync(fileItemId);
                await DeleteFileItemDependenciesAsync(fileItem);
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

            var restored = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(previous.Id)
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
            Guid projectId, FileVersionState version, Guid actorId, bool isSystemAdmin)
        {
            if (isSystemAdmin) return;
            if (version.UploadedByAccountId == actorId) return;

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId);
            if (project?.ManagerAccountId == actorId) return;

            throw new ApiExceptionResponse(
                "Chỉ người tải lên, quản lý dự án hoặc quản trị hệ thống được xoá tệp này.", 403);
        }

        private async Task RequireNoOpenIssueAsync(Guid fileItemId)
        {
            var openIssues = (await _unitOfWork.Repository<Issue>().FindAsync(
                    i => i.LinkedFileItemId == fileItemId && i.Status != IssueStatus.Closed))
                .Count();
            if (openIssues > 0)
                throw new ApiExceptionResponse(
                    $"Tệp còn {openIssues} vấn đề chưa đóng. Đóng hết rồi mới xoá được.", 409);
        }

        private async Task RequireNoPendingApprovalAsync(Guid fileItemId)
        {
            var pending = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.FileItemId == fileItemId && a.Status == ApprovalRequestStatus.Pending))
                .Any();
            if (pending)
                throw new ApiExceptionResponse("Tệp đang có phiếu duyệt chờ xử lý nên không xoá được.", 409);
        }

        private async Task RequireNoApprovalHistoryAsync(Guid fileItemId)
        {
            var hasApproval = (await _unitOfWork.Repository<ApprovalRequest>()
                .FindAsync(a => a.FileItemId == fileItemId)).Any();
            if (hasApproval)
                throw new ApiExceptionResponse(
                    "Tệp đã từng qua phiếu duyệt nên không xoá khỏi hệ thống được.", 409);
        }

        private async Task RequireNoReturnRequestAsync(Guid fileItemId)
        {
            var hasReturnRequest = (await _unitOfWork.Repository<ZoneReturnRequest>()
                .FindAsync(r => r.FileItemId == fileItemId)).Any();
            if (hasReturnRequest)
                throw new ApiExceptionResponse(
                    "Tệp có yêu cầu trả về vùng WIP nên không xoá được.", 409);
        }

        private async Task DeleteVersionDependenciesAsync(Guid versionId)
        {
            var loiChecks = await _unitOfWork.Repository<FileVersionLoiCheck>()
                .FindAsync(c => c.FileVersionId == versionId);
            foreach (var check in loiChecks)
                _unitOfWork.Repository<FileVersionLoiCheck>().Delete(check);

            var markupSets = (await _unitOfWork.Repository<MarkupSet>()
                .FindAsync(s => s.FileVersionId == versionId)).ToList();
            var setIds = markupSets.Select(s => s.Id).ToHashSet();

            var notes = await _unitOfWork.Repository<FileNote>()
                .FindAsync(n => n.FileVersionId == versionId || setIds.Contains(n.MarkupSetId));
            foreach (var note in notes)
                _unitOfWork.Repository<FileNote>().Delete(note);

            foreach (var set in markupSets)
                _unitOfWork.Repository<MarkupSet>().Delete(set);
        }

        private async Task DeleteFileItemDependenciesAsync(FileItem fileItem)
        {
            var closedIssues = (await _unitOfWork.Repository<Issue>()
                .FindAsync(i => i.LinkedFileItemId == fileItem.Id)).ToList();
            foreach (var issue in closedIssues)
            {
                issue.LinkedFileItemId = null;
                _unitOfWork.Repository<Issue>().Update(issue);
            }

            var links = (await _unitOfWork.Repository<FileLink>()
                .FindAsync(l => l.FileItemId == fileItem.Id || l.LinkedFileItemId == fileItem.Id)).ToList();
            foreach (var link in links)
                _unitOfWork.Repository<FileLink>().Delete(link);

            var permissions = await _unitOfWork.Repository<FilePermission>()
                .FindAsync(p => p.FileItemId == fileItem.Id);
            foreach (var permission in permissions)
                _unitOfWork.Repository<FilePermission>().Delete(permission);

            var namingMetadata = await _unitOfWork.Repository<FileNamingMetadata>()
                .FindAsync(m => m.FileItemId == fileItem.Id);
            foreach (var metadata in namingMetadata)
                _unitOfWork.Repository<FileNamingMetadata>().Delete(metadata);

            var signaturePositions = await _unitOfWork.Repository<FileSignaturePosition>()
                .FindAsync(p => p.FileItemId == fileItem.Id);
            foreach (var position in signaturePositions)
                _unitOfWork.Repository<FileSignaturePosition>().Delete(position);

            var viewGrants = await _unitOfWork.Repository<FileViewGrant>()
                .FindAsync(g => g.FileItemId == fileItem.Id);
            foreach (var grant in viewGrants)
                _unitOfWork.Repository<FileViewGrant>().Delete(grant);

            var documents = await _unitOfWork.Repository<Document>()
                .FindAsync(d => d.FileItemId == fileItem.Id);
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
