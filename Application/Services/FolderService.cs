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

            var subtree = await GetSubtreeTopDownAsync(entity);
            var subtreeIds = subtree.Select(f => f.Id).ToList();

            var documents = await _unitOfWork.Repository<FileItem>()
                .FindAsync(f => subtreeIds.Contains(f.FolderId));
            var documentCount = documents.Count();
            if (documentCount > 0)
            {
                throw new ApiExceptionResponse(
                    $"Folder still contains {documentCount} document(s).",
                    409);
            }

            for (var i = subtree.Count - 1; i >= 0; i--)
            {
                _unitOfWork.Repository<Folder>().Delete(subtree[i]);
            }

            var subFolderCount = subtree.Count - 1;
            var subFolderNote = subFolderCount > 0 ? $" cùng {subFolderCount} thư mục con" : string.Empty;
            await _auditLog.LogAsync(
                LogScope.Group, AuditAction.Delete, nameof(Folder), entity.Id.ToString(), actorId,
                detail: $"Xoá thư mục '{entity.Name}' (vùng {entity.Area}){subFolderNote}",
                projectId: entity.ProjectId, folderId: entity.Id);

            await _unitOfWork.CommitAsync();
        }

        private async Task<List<Folder>> GetSubtreeTopDownAsync(Folder root)
        {
            var projectFolders = await _unitOfWork.Repository<Folder>()
                .FindAsync(f => f.ProjectId == root.ProjectId);

            var childrenByParent = projectFolders
                .Where(f => f.ParentFolderId.HasValue)
                .GroupBy(f => f.ParentFolderId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var ordered = new List<Folder>();
            var pending = new Queue<Folder>();
            pending.Enqueue(root);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                ordered.Add(current);

                if (!childrenByParent.TryGetValue(current.Id, out var children)) continue;
                foreach (var child in children) pending.Enqueue(child);
            }

            return ordered;
        }
    }
}
