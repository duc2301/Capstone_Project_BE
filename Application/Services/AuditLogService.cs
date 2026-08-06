using Application.DTOs.RequestDTOs.Audit;
using Application.DTOs.ResponseDTOs.Audit;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using System.Text;
using Domain.Entities;
using Domain.Enum.Audit;

namespace Application.Services
{
    public class AuditLogService : IAuditLogService
    {
        private const int ExportMaxRows = 5000;
        private const int VietnamUtcOffsetHours = 7;

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

            filter.EntityType = nameof(FileItem);
            filter.EntityId = fileItemId.ToString();

            return await _auditLogRepository.QueryAsync(filter, folder.ProjectId, null, null);
        }

        public async Task<AuditLogExportDTO> ExportCsvAsync(
            Guid? projectId, AuditLogFilterDTO filter, Guid actorId)
        {
            if (projectId.HasValue)
                await EnsureCanReadProjectAsync(projectId.Value, actorId);
            else if (!await _permission.HasSystemAdminAsync(actorId))
                throw new ApiExceptionResponse("Only system admin can export system audit logs.", 403);

            var scopeProjectId = projectId ?? filter.ProjectId;
            var rows = await _auditLogRepository.QueryAllAsync(
                filter, scopeProjectId, null, null, ExportMaxRows);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
            var prefix = projectId.HasValue ? "nhat-ky-du-an" : "nhat-ky-he-thong";

            return new AuditLogExportDTO
            {
                Content = BuildCsv(rows),
                FileName = $"{prefix}-{stamp}.csv",
                ContentType = "text/csv"
            };
        }

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

        private static byte[] BuildCsv(List<AuditLogResponseDTO> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Thời gian (UTC+7),Người thao tác,Hành động,Phạm vi,Đối tượng,Mã đối tượng,Nội dung");

            foreach (var row in rows)
            {
                sb.Append(CsvCell(row.CreatedAt?.AddHours(VietnamUtcOffsetHours).ToString("yyyy-MM-dd HH:mm:ss"))).Append(',');
                sb.Append(CsvCell(row.ActorName)).Append(',');
                sb.Append(CsvCell(row.Action.ToString())).Append(',');
                sb.Append(CsvCell(row.Scope.ToString())).Append(',');
                sb.Append(CsvCell(row.EntityType)).Append(',');
                sb.Append(CsvCell(row.EntityId)).Append(',');
                sb.AppendLine(CsvCell(row.Detail));
            }

            var utf8Bom = new UTF8Encoding(true);
            return utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes(sb.ToString())).ToArray();
        }

        private static string CsvCell(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
