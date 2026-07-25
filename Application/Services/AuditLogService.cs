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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IFolderTreeRepository _folderTreeRepository;

        public AuditLogService(
            IUnitOfWork unitOfWork,
            IAuditLogRepository auditLogRepository,
            IFolderTreeRepository folderTreeRepository)
        {
            _unitOfWork = unitOfWork;
            _auditLogRepository = auditLogRepository;
            _folderTreeRepository = folderTreeRepository;
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

        public async Task<AuditLogPageDTO> GetSystemAsync(AuditLogFilterDTO filter, bool isSystemAdmin)
        {
            if (!isSystemAdmin)
                throw new ApiExceptionResponse("Only system admin can view system audit logs.", 403);

            // filter.ProjectId cho phép admin lọc về 1 dự án; null = mọi dự án.
            return await _auditLogRepository.QueryAsync(filter, filter.ProjectId, null, null);
        }

        public async Task<AuditLogPageDTO> GetByProjectAsync(
            Guid projectId, AuditLogFilterDTO filter, Guid actorId, bool isSystemAdmin)
        {
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var isManager = project.ManagerAccountId == actorId;
            var isProjectAdmin = await _folderTreeRepository.HasFullAccessAsync(projectId, actorId);

            if (!isSystemAdmin && !isManager && !isProjectAdmin)
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
            var viewableFolderIds = await _folderTreeRepository.GetViewableFolderIdsAsync(projectId, actorId);
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
    }
}
