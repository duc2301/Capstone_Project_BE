using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Application.Services
{
    /// <summary>
    /// Niêm phong lưu trữ (Published → Archived). Tách khỏi luồng phê duyệt:
    /// chỉ PM/Admin chủ động chốt bản Published chính thức vào vùng Archived, bấm được nhiều lần.
    /// Mỗi lần niêm phong tạo/cộng dồn 1 bản lưu (mirror) chỉ-đọc trỏ cùng blob với bản Published gốc.
    /// </summary>
    public class FileArchiveService : IFileArchiveService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileZoneResolverService _zoneResolver;
        private readonly IFileVersionService _fileVersionService;
        private readonly IAuditLogService _auditLog;

        public FileArchiveService(
            IUnitOfWork unitOfWork,
            IFileZoneResolverService zoneResolver,
            IFileVersionService fileVersionService,
            IAuditLogService auditLog)
        {
            _unitOfWork = unitOfWork;
            _zoneResolver = zoneResolver;
            _fileVersionService = fileVersionService;
            _auditLog = auditLog;
        }

        public async Task<Guid> SealToArchiveAsync(Guid fileItemId, Guid actor, string actorRole)
        {
            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(file.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            // 1) Chỉ niêm phong file ĐANG Ở PUBLISHED.
            if (folder.Area != CdeArea.Published)
                throw new ApiExceptionResponse("Chỉ niêm phong lưu trữ file đang ở Published.", 400);

            // 2) Chỉ PM (Project.ManagerAccountId) hoặc Admin hệ thống.
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(folder.ProjectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);
            var isAdmin = actorRole == AccountRole.Admin.ToString();
            if (!isAdmin && project.ManagerAccountId != actor)
                throw new ApiExceptionResponse("Chỉ PM hoặc Admin được niêm phong lưu trữ.", 403);

            // 3) Không niêm phong khi đang có yêu cầu duyệt treo (đảm bảo bản Published đã ổn định).
            var hasPending = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.FileItemId == file.Id && a.Status == ApprovalRequestStatus.Pending)).Any();
            if (hasPending)
                throw new ApiExceptionResponse("File đang chờ duyệt, không thể niêm phong.", 409);

            // 4) Lấy bản Published hiện hành để niêm phong.
            var currentPub = file.CurrentVersionId.HasValue
                ? await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(file.CurrentVersionId.Value)
                : null;
            if (currentPub is null || currentPub.Stage != VersionStage.Published)
                throw new ApiExceptionResponse("File chưa có bản Published để niêm phong.", 400);

            // 5) Resolve folder Archived tương ứng (mirror theo nhóm) — dùng lại zoneResolver như luồng move cũ.
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(folder.ProjectId);
            var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(file, folder, projectFolders);
            var archivedFolder = await _zoneResolver.ResolveTargetFolderAsync(
                folder, CdeArea.Archived, teamGroupIds, projectFolders, "Archived folder not found.");

            // 6) Tìm bản lưu đã có (cộng dồn) hay tạo mới.
            var mirror = (await _unitOfWork.Repository<FileItem>().FindAsync(
                    f => f.SourceFileItemId == file.Id)).FirstOrDefault();

            if (mirror != null)
            {
                // Idempotency: bản Published hiện tại đã được niêm phong rồi thì không nhân đôi.
                var mirrorCurrent = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(mirror.Id);
                if (mirrorCurrent != null && mirrorCurrent.PublishedRevision == currentPub.PublishedRevision)
                    throw new ApiExceptionResponse("Phiên bản chính thức này đã được niêm phong lưu trữ.", 409);
            }
            else
            {
                mirror = new FileItem
                {
                    Id = Guid.NewGuid(),
                    FolderId = archivedFolder.Id,
                    Name = file.Name,
                    FileType = file.FileType,
                    Status = FileItemStatus.Approved,
                    SourceFileItemId = file.Id,
                    CreatedByAccountId = actor,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<FileItem>().CreateAsync(mirror);
            }

            // 7) Append 1 dòng version copy nội dung + số hiệu từ bản Published gốc.
            var result = await _fileVersionService.AppendArchivedVersionAsync(mirror.Id, currentPub);
            mirror.CurrentVersionId = result.VersionStateId;
            mirror.UpdatedAt = DateTime.UtcNow;

            // folderId = folder Archived đích -> log hiện đúng ở view của người xem vùng lưu trữ.
            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Archive, nameof(FileItem), mirror.Id.ToString(), actor,
                detail: $"Niêm phong lưu trữ '{file.Name}' ({currentPub.DisplayVersion})",
                projectId: folder.ProjectId, folderId: archivedFolder.Id);

            await _unitOfWork.CommitAsync();
            return mirror.Id;
        }
    }
}
