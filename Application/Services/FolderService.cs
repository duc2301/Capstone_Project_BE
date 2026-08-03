using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;

using Domain.Entities;
using Domain.Enum.Audit;

namespace Application.Services
{
    public class FolderService : IFolderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLog;
        private readonly IPermissionCheckingService _permission;

        public FolderService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IAuditLogService auditLog,
            IPermissionCheckingService permission)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _auditLog = auditLog;
            _permission = permission;
        }

        public async Task<IEnumerable<FolderResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<FolderResponseDTO>>(
                await _unitOfWork.Repository<Folder>().GetAllAsync());

        public async Task<FolderResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Folder>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<FolderResponseDTO>(entity);
        }

        public async Task<FolderResponseDTO> CreateAsync(CreateFolderDTO dto, Guid actorId)
        {
            var entity = _mapper.Map<Folder>(dto);
            entity.Id = Guid.NewGuid();
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<Folder>().CreateAsync(entity);

            await _auditLog.LogAsync(
                LogScope.Group, AuditAction.Create, nameof(Folder), entity.Id.ToString(), actorId,
                detail: $"Tạo thư mục '{entity.Name}' (vùng {entity.Area})",
                projectId: entity.ProjectId, folderId: entity.Id);

            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderResponseDTO>(entity);
        }

        public async Task<FolderResponseDTO> UpdateAsync(Guid id, UpdateFolderDTO dto, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<Folder>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Folder with ID {id} not found.", 404);

            await _permission.CanEditFolderAsync(id, actorId);

            _mapper.Map(dto, entity);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Folder>().Update(entity);

            await _auditLog.LogAsync(
                LogScope.Group, AuditAction.Update, nameof(Folder), entity.Id.ToString(), actorId,
                detail: $"Cập nhật thư mục '{entity.Name}'",
                projectId: entity.ProjectId, folderId: entity.Id);

            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<Folder>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Folder with ID {id} not found.", 404);

            await _permission.CanEditFolderAsync(id, actorId);

            _unitOfWork.Repository<Folder>().Delete(entity);

            await _auditLog.LogAsync(
                LogScope.Group, AuditAction.Delete, nameof(Folder), entity.Id.ToString(), actorId,
                detail: $"Xoá thư mục '{entity.Name}' (vùng {entity.Area})",
                projectId: entity.ProjectId, folderId: entity.Id);

            await _unitOfWork.CommitAsync();
        }
    }
}
