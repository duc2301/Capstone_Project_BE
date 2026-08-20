using Application.DTOs.RequestDTOs.Audit;
using Application.DTOs.ResponseDTOs.Audit;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;

namespace Application.Services
{
    public class AuditLogService : IAuditLogService
    {
        private const int ExportMaxRows = 5000;
        private const string SystemExportPrefix = "nhat-ky-he-thong";
        private const string ProjectExportPrefix = "nhat-ky-du-an";
        private const string ExportExtension = "xlsx";
        private const int VietnamUtcOffsetHours = 7;
        private const string ExcelContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private static readonly HashSet<string> FileActivityEntityTypes = new()
        {
            nameof(FileItem),
            nameof(ApprovalRequest),
            nameof(ZoneReturnRequest),
            nameof(MarkupSet),
            nameof(Issue)
        };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IPermissionCheckingService _permission;

        public AuditLogService(
            IUnitOfWork unitOfWork,
            IAuditLogRepository auditLogRepository,
            IPermissionCheckingService permission)
        {
            _unitOfWork = unitOfWork;
            _auditLogRepository = auditLogRepository;
            _permission = permission;
        }

        // ===== GHI =====

        public async Task LogAsync(
            LogScope scope,
            AuditAction action,
            string entityType,
            string entityId,
            Guid? actorId,
            string? detail = null,
            Guid? projectId = null,
            Guid? folderId = null,
            Guid? groupId = null)
        {
            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                Scope = scope,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                ActorAccountId = actorId,
                Detail = detail,
                ProjectId = projectId,
                FolderId = folderId,
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<AuditLog>().CreateAsync(log);
            // Không CommitAsync — caller commit chung transaction nghiệp vụ.
        }

        public async Task LogAndSaveAsync(
            LogScope scope,
            AuditAction action,
            string entityType,
            string entityId,
            Guid? actorId,
            string? detail = null,
            Guid? projectId = null,
            Guid? folderId = null,
            Guid? groupId = null)
        {
            await LogAsync(scope, action, entityType, entityId, actorId,
                           detail, projectId, folderId, groupId);
            await _unitOfWork.CommitAsync();
        }

        // ===== ĐỌC =====

        public async Task<AuditLogPageDTO> GetSystemAsync(AuditLogFilterDTO filter, Guid actorId)
        {
            if (!await _permission.HasSystemAdminAsync(actorId))
                throw new ApiExceptionResponse("Only system admin can view system audit logs.", 403);

            // filter.ProjectId cho phép admin lọc về 1 dự án; null = mọi dự án.
            return await _auditLogRepository.QueryAsync(filter, filter.ProjectId, null, null);
        }

        public async Task<AuditLogPageDTO> GetByProjectAsync(
            Guid projectId, AuditLogFilterDTO filter, Guid actorId)
        {
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            // Manager là chủ dự án (Project.ManagerAccountId) — quy tắc riêng của audit, không phải ACL.
            var isManager = project.ManagerAccountId == actorId;
            var hasFullAccess = await _permission.HasProjectFullAccessAsync(projectId, actorId);

            if (!hasFullAccess && !isManager)
                throw new ApiExceptionResponse(
                    "Only system admin or the project manager can view the project audit log.", 403);

            return await _auditLogRepository.QueryAsync(filter, projectId, null, null);
        }

        public async Task<AuditLogPageDTO> GetMyInProjectAsync(
            Guid projectId, AuditLogFilterDTO filter, Guid actorId)
        {
            _ = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            // Mặc định từ chối: chỉ thấy log gắn folder mình có quyền View, hoặc gắn nhóm mình.
            var viewableFolderIds = await _permission.GetViewableFolderIdsAsync(projectId, actorId);
            var myGroupIds = await _auditLogRepository.GetMyActiveGroupIdsAsync(projectId, actorId);

            // Không thuộc nhóm nào và không được xem folder nào -> trang rỗng (không lộ dữ liệu).
            if (viewableFolderIds.Count == 0 && myGroupIds.Count == 0)
            {
                return new AuditLogPageDTO
                {
                    Items = new List<AuditLogResponseDTO>(),
                    Total = 0,
                    Page = filter.Page < 1 ? 1 : filter.Page,
                    PageSize = filter.PageSize < 1 || filter.PageSize > 200 ? 20 : filter.PageSize
                };
            }

            return await _auditLogRepository.QueryAsync(filter, projectId, viewableFolderIds, myGroupIds);
        }

        public async Task<AuditLogPageDTO> GetMyActivityAsync(AuditLogFilterDTO filter, Guid actorId)
        {
            filter.ActorId = actorId;
            return await _auditLogRepository.QueryAsync(filter, filter.ProjectId, null, null);
        }

        public async Task<AuditLogPageDTO> GetByFileItemAsync(
            Guid fileItemId, AuditLogFilterDTO filter, Guid actorId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            if (!await _permission.HasViewFileAsync(fileItemId, actorId))
                throw new ApiExceptionResponse("You do not have permission to view this file.", 403);

            var entityIds = await CollectFileEntityIdsAsync(fileItemId);

            return await _auditLogRepository.QueryByEntitiesAsync(
                filter, folder.ProjectId, FileActivityEntityTypes, entityIds);
        }

        private async Task<HashSet<string>> CollectFileEntityIdsAsync(Guid fileItemId)
        {
            var ids = new HashSet<string> { fileItemId.ToString() };

            await AddIdsAsync<FileItem>(ids, f => f.SourceFileItemId == fileItemId, f => f.Id);
            await AddIdsAsync<ApprovalRequest>(ids, a => a.FileItemId == fileItemId, a => a.Id);
            await AddIdsAsync<ZoneReturnRequest>(ids, r => r.FileItemId == fileItemId, r => r.Id);
            await AddIdsAsync<MarkupSet>(ids, m => m.FileItemId == fileItemId, m => m.Id);
            await AddIdsAsync<Issue>(ids, i => i.LinkedFileItemId == fileItemId, i => i.Id);

            return ids;
        }

        private async Task AddIdsAsync<TEntity>(
            HashSet<string> ids,
            System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate,
            Func<TEntity, Guid> idSelector) where TEntity : class
        {
            var found = await _unitOfWork.Repository<TEntity>().FindAsync(predicate);
            foreach (var entity in found)
                ids.Add(idSelector(entity).ToString());
        }

        public async Task<AuditLogExportDTO> ExportAsync(
            Guid? projectId, AuditLogFilterDTO filter, Guid actorId)
        {
            if (projectId.HasValue)
                await EnsureCanReadProjectAsync(projectId.Value, actorId);
            else if (!await _permission.HasSystemAdminAsync(actorId))
                throw new ApiExceptionResponse("Only system admin can export system audit logs.", 403);

            var scopeProjectId = projectId ?? filter.ProjectId;
            var rows = await _auditLogRepository.QueryAllAsync(
                filter, scopeProjectId, null, null, ExportMaxRows + 1);

            var truncated = rows.Count > ExportMaxRows;
            if (truncated)
                rows = rows.Take(ExportMaxRows).ToList();

            var scopedProjectName = scopeProjectId.HasValue
                ? (await _unitOfWork.Repository<Project>().GetByIdAsync(scopeProjectId.Value))?.ProjectName
                : null;

            var exportedAtLocal = DateTime.UtcNow.AddHours(VietnamUtcOffsetHours);
            var content = AuditLogWorkbookBuilder.Build(new AuditLogWorkbookRequest(
                Title: scopedProjectName is null
                    ? "NHẬT KÝ HOẠT ĐỘNG HỆ THỐNG"
                    : $"NHẬT KÝ HOẠT ĐỘNG DỰ ÁN — {scopedProjectName}",
                ExportedBy: await ResolveAccountNameAsync(actorId) ?? actorId.ToString(),
                ExportedAtLocal: exportedAtLocal,
                FilterLines: await BuildFilterLinesAsync(filter, projectId.HasValue ? null : scopedProjectName),
                Truncated: truncated,
                TruncationLimit: ExportMaxRows,
                LocalOffsetHours: VietnamUtcOffsetHours,
                Rows: rows));

            return new AuditLogExportDTO
            {
                Content = content,
                FileName = BuildExportFileName(scopeProjectId, scopedProjectName, exportedAtLocal),
                ContentType = ExcelContentType
            };
        }

        private static string BuildExportFileName(
            Guid? scopeProjectId, string? projectName, DateTime exportedAtLocal) =>
            scopeProjectId.HasValue
                ? FileDownloadNaming.BuildTimestampedName(
                    ProjectExportPrefix, projectName, exportedAtLocal, ExportExtension)
                : FileDownloadNaming.BuildTimestampedName(
                    SystemExportPrefix, null, exportedAtLocal, ExportExtension);

        private async Task<List<string>> BuildFilterLinesAsync(AuditLogFilterDTO filter, string? projectName)
        {
            var lines = new List<string>();

            if (projectName is not null)
                lines.Add($"Dự án: {projectName}");
            if (filter.Scope.HasValue)
                lines.Add($"Phạm vi: {AuditLogWorkbookBuilder.ScopeLabel(filter.Scope.Value)}");
            if (filter.Action.HasValue)
                lines.Add($"Hành động: {AuditLogWorkbookBuilder.ActionLabel(filter.Action.Value)}");
            if (filter.ActorId.HasValue)
                lines.Add($"Người thao tác: {await ResolveAccountNameAsync(filter.ActorId.Value) ?? filter.ActorId.Value.ToString()}");
            if (!string.IsNullOrWhiteSpace(filter.EntityType))
                lines.Add($"Đối tượng: {AuditLogWorkbookBuilder.EntityLabel(filter.EntityType)}");
            if (!string.IsNullOrWhiteSpace(filter.Search))
                lines.Add($"Từ khoá: \"{filter.Search}\"");
            if (filter.From.HasValue)
                lines.Add($"Từ ngày: {filter.From.Value.AddHours(VietnamUtcOffsetHours):dd/MM/yyyy}");
            if (filter.To.HasValue)
                lines.Add($"Đến ngày: {filter.To.Value.AddHours(VietnamUtcOffsetHours):dd/MM/yyyy}");

            return lines;
        }

        private async Task<string?> ResolveAccountNameAsync(Guid accountId) =>
            (await _unitOfWork.Repository<Account>().GetByIdAsync(accountId))?.UserName;

        private async Task EnsureCanReadProjectAsync(Guid projectId, Guid actorId)
        {
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var isManager = project.ManagerAccountId == actorId;
            var hasFullAccess = await _permission.HasProjectFullAccessAsync(projectId, actorId);

            if (!hasFullAccess && !isManager)
                throw new ApiExceptionResponse(
                    "Only system admin or the project manager can export the project audit log.", 403);
        }

    }
}
