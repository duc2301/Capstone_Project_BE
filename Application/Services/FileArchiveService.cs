using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Permission;

namespace Application.Services
{
    /// <summary>
    /// Niêm phong lưu trữ (Published → Archived). Tách khỏi luồng phê duyệt:
    /// chỉ PM/Admin chủ động chốt bản Published chính thức vào vùng Archived, bấm được nhiều lần.
    /// Mỗi lần niêm phong tạo/cộng dồn 1 bản lưu (mirror) chỉ-đọc trỏ cùng blob với bản Published gốc,
    /// và chép theo ACL cấp file của bản gốc (xem SyncMirrorPermissionsAsync).
    /// Bản lưu được ingest vào chỉ mục ngữ nghĩa để khi bản Published bị rút về WIP, tìm kiếm vẫn
    /// rơi về được bản chính thức gần nhất (xem DocumentSearchRepository.SearchByVectorAsync).
    /// </summary>
    public class FileArchiveService : IFileArchiveService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileZoneResolverService _zoneResolver;
        private readonly IFileVersionService _fileVersionService;
        private readonly IAuditLogService _auditLog;
        private readonly IDocumentIndexSyncService _indexSync;

        public FileArchiveService(
            IUnitOfWork unitOfWork,
            IFileZoneResolverService zoneResolver,
            IFileVersionService fileVersionService,
            IAuditLogService auditLog,
            IDocumentIndexSyncService indexSync)
        {
            _unitOfWork = unitOfWork;
            _zoneResolver = zoneResolver;
            _fileVersionService = fileVersionService;
            _auditLog = auditLog;
            _indexSync = indexSync;
        }

        public async Task<Guid> SealToArchiveAsync(Guid fileItemId, Guid actor, string actorRole)
        {
            var (file, folder) = await LoadFileAndFolderAsync(fileItemId);

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
            var currentPub = await GetCurrentPublishedVersionAsync(file);
            if (currentPub is null)
                throw new ApiExceptionResponse("File chưa có bản Published để niêm phong.", 400);

            var mirrorId = await SealCoreAsync(file, folder, currentPub, actor, automatic: false);
            if (mirrorId is null)
                throw new ApiExceptionResponse("Phiên bản chính thức này đã được niêm phong lưu trữ.", 409);

            await _unitOfWork.CommitAsync();

            // Ingest bản lưu ra chỉ mục ngữ nghĩa: xin index SAU commit (mirror.CurrentVersionId đã set trong
            // SealCoreAsync) để worker đọc được version vừa append.
            await _indexSync.RequestIndexAsync(mirrorId.Value);

            return mirrorId.Value;
        }

        public async Task<Guid?> SealForZoneReturnAsync(Guid fileItemId, Guid actorId)
        {
            var (file, folder) = await LoadFileAndFolderAsync(fileItemId);

            // Trả về WIP từ Shared: chưa từng có bản chính thức nên không có gì để niêm phong.
            if (folder.Area != CdeArea.Published)
                return null;

            // Không kiểm quyền PM/Admin: người bấm là Team Leader duyệt yêu cầu trả vùng, và việc niêm
            // phong ở đây là hệ quả hệ thống chứ không phải một hành động chủ động của họ.
            // Cũng không chặn theo ApprovalRequest treo: mục tiêu là không đánh mất bản chính thức,
            // chặn ở đây chỉ khiến cả luồng trả vùng hỏng theo.
            var currentPub = await GetCurrentPublishedVersionAsync(file);
            if (currentPub is null)
                return null;

            // KHÔNG commit — caller gộp chung một transaction với việc trả vùng.
            return await SealCoreAsync(file, folder, currentPub, actorId, automatic: true);
        }

        // ---------- nội bộ ----------

        /// <summary>
        /// Lõi niêm phong: resolve folder Archived, tạo/tìm bản lưu, append version, ghi audit.
        /// KHÔNG commit và KHÔNG xin index — hai việc đó thuộc về caller vì ranh giới transaction
        /// mỗi luồng một khác. Trả null khi bản Published này đã được niêm phong rồi (idempotent).
        /// </summary>
        private async Task<Guid?> SealCoreAsync(
            FileItem file, Folder folder, FileVersionState currentPub, Guid actor, bool automatic)
        {
            // Resolve folder Archived tương ứng (mirror theo nhóm) — dùng lại zoneResolver như luồng move cũ.
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(folder.ProjectId);
            var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(file, folder, projectFolders);
            var archivedFolder = await _zoneResolver.ResolveTargetFolderAsync(
                folder, CdeArea.Archived, teamGroupIds, projectFolders, "Archived folder not found.");

            // Tìm bản lưu đã có (cộng dồn) hay tạo mới.
            var mirror = (await _unitOfWork.Repository<FileItem>().FindAsync(
                    f => f.SourceFileItemId == file.Id)).FirstOrDefault();

            if (mirror != null)
            {
                // Idempotency: bản Published hiện tại đã được niêm phong rồi thì không nhân đôi.
                var mirrorCurrent = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(mirror.Id);
                if (mirrorCurrent != null && mirrorCurrent.PublishedRevision == currentPub.PublishedRevision)
                    return null;
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

            // Bản lưu là một FileItem KHÁC nên không thừa hưởng dòng FilePermission nào của bản gốc.
            // Không chép thì mọi lệnh cấm cấp file biến mất khi niêm phong và bản lưu rơi về ACL thư
            // mục Archived — vốn mở CanView cho cả nhóm (FolderBootstrapService.BuildDefaultGroupPermission).
            await SyncMirrorPermissionsAsync(file.Id, mirror.Id);

            // Append 1 dòng version copy nội dung + số hiệu từ bản Published gốc.
            var result = await _fileVersionService.AppendArchivedVersionAsync(mirror.Id, currentPub);
            mirror.CurrentVersionId = result.VersionStateId;
            // Mô tả/cảnh báo của bản lưu do AppendArchivedVersionAsync copy sẵn từ version Published gốc (per-version).
            mirror.UpdatedAt = DateTime.UtcNow;

            // folderId = folder Archived đích -> log hiện đúng ở view của người xem vùng lưu trữ.
            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Archive, nameof(FileItem), mirror.Id.ToString(), actor,
                detail: automatic
                    ? $"Tự động niêm phong lưu trữ '{file.Name}' ({currentPub.DisplayVersion}) trước khi trả về WIP"
                    : $"Niêm phong lưu trữ '{file.Name}' ({currentPub.DisplayVersion})",
                projectId: folder.ProjectId, folderId: archivedFolder.Id);

            return mirror.Id;
        }

        /// <summary>
        /// Chép ACL cấp file của bản gốc sang bản lưu, chạy ở MỖI lần niêm phong vì quyền của bản gốc
        /// có thể đã đổi giữa hai lần chốt. Chỉ chép quyền ĐỌC: bản lưu là hồ sơ chỉ-đọc nên
        /// CanEdit/CanApprove luôn false, kể cả khi bản gốc có.
        /// Đây là ảnh chụp tại thời điểm niêm phong — sửa quyền bản gốc SAU đó không tự lan sang bản
        /// lưu; muốn đổi thì phân quyền thẳng trên bản lưu (nó là FileItem bình thường trong Archived).
        /// </summary>
        private async Task SyncMirrorPermissionsAsync(Guid sourceFileItemId, Guid mirrorFileItemId)
        {
            var sourceAcl = (await _unitOfWork.Repository<FilePermission>().FindAsync(
                    p => p.FileItemId == sourceFileItemId && p.Status == PermissionStatus.Active))
                .ToList();
            var mirrorAcl = (await _unitOfWork.Repository<FilePermission>().FindAsync(
                    p => p.FileItemId == mirrorFileItemId))
                .ToList();

            if (sourceAcl.Count == 0 && mirrorAcl.Count == 0)
                return;

            // Mỗi dòng ACL gán cho ĐÚNG MỘT chủ thể: nhóm (ProjectParticipantId) hoặc tài khoản
            // (AccountId) — xem ràng buộc CHECK ở FilePermission. Cặp hai cột là khoá đối chiếu.
            static (Guid?, Guid?) SubjectOf(FilePermission acl) => (acl.ProjectParticipantId, acl.AccountId);

            var mirrorBySubject = new Dictionary<(Guid?, Guid?), FilePermission>();
            foreach (var acl in mirrorAcl)
                mirrorBySubject[SubjectOf(acl)] = acl;

            foreach (var source in sourceAcl)
            {
                if (mirrorBySubject.TryGetValue(SubjectOf(source), out var existing))
                {
                    existing.CanView = source.CanView;
                    existing.CanEdit = false;
                    existing.CanApprove = true;
                    existing.Status = PermissionStatus.Active;
                    _unitOfWork.Repository<FilePermission>().Update(existing);
                    continue;
                }

                await _unitOfWork.Repository<FilePermission>().CreateAsync(new FilePermission
                {
                    Id = Guid.NewGuid(),
                    FileItemId = mirrorFileItemId,
                    ProjectParticipantId = source.ProjectParticipantId,
                    AccountId = source.AccountId,
                    CanView = source.CanView,
                    CanEdit = false,
                    CanApprove = true,
                    Status = PermissionStatus.Active
                });
            }

            // Dòng bản gốc đã gỡ (hoặc đã tắt) -> tắt bên bản lưu chứ không xoá, để còn dấu vết là
            // quyền này từng tồn tại. Bản lưu quay về ACL thư mục Archived đúng như bản gốc.
            var sourceSubjects = sourceAcl.Select(SubjectOf).ToHashSet();
            foreach (var stale in mirrorAcl)
            {
                if (stale.Status != PermissionStatus.Active || sourceSubjects.Contains(SubjectOf(stale)))
                    continue;

                stale.Status = PermissionStatus.Inactive;
                _unitOfWork.Repository<FilePermission>().Update(stale);
            }
        }

        private async Task<(FileItem File, Folder Folder)> LoadFileAndFolderAsync(Guid fileItemId)
        {
            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(file.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            return (file, folder);
        }

        private async Task<FileVersionState?> GetCurrentPublishedVersionAsync(FileItem file)
        {
            if (!file.CurrentVersionId.HasValue)
                return null;

            var current = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(file.CurrentVersionId.Value);
            return current?.Stage == VersionStage.Published ? current : null;
        }
    }
}
