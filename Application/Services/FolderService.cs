using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;

using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Cde;

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

            var newName = (dto.Name ?? string.Empty).Trim();
            if (newName.Length == 0)
                throw new ApiExceptionResponse("Folder name is required.", 400);

            var projectFolders = await GetProjectFoldersAsync(entity.ProjectId);
            var mirrorGroup = ResolveMirrorGroup(entity, projectFolders);

            EnsureNameAvailable(newName, mirrorGroup, projectFolders);

            var previousName = entity.Name;
            var now = DateTime.UtcNow;
            foreach (var folder in mirrorGroup)
            {
                folder.Name = newName;
                folder.UpdatedAt = now;
                _unitOfWork.Repository<Folder>().Update(folder);
            }

            // Nói rõ đổi gì thành gì và lan sang những khu vực NÀO (đọc "ở 4 khu vực" thì vẫn phải đoán
            // là khu vực nào). Trường hợp tên không đổi cũng ghi thẳng ra thay vì "đổi X thành X".
            var areaNames = string.Join(", ", mirrorGroup
                .Select(f => f.Area == CdeArea.Wip ? "WIP" : f.Area.ToString())
                .Distinct());
            var changeDetail = previousName == newName
                ? $"Lưu thư mục '{newName}': tên không thay đổi"
                : $"Đổi tên thư mục '{previousName}' thành '{newName}' (áp cho khu vực: {areaNames})";

            await _auditLog.LogAsync(
                LogScope.Group, AuditAction.Update, nameof(Folder), entity.Id.ToString(), actorId,
                detail: changeDetail,
                projectId: entity.ProjectId, folderId: entity.Id);

            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<Folder>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Folder with ID {id} not found.", 404);

            await _permission.CanEditFolderAsync(id, actorId);

            var projectFolders = await GetProjectFoldersAsync(entity.ProjectId);
            var mirrorGroup = ResolveMirrorGroup(entity, projectFolders);
            var childrenByParent = GroupByParent(projectFolders);

            var subtrees = mirrorGroup
                .Select(folder => new
                {
                    Folder = folder,
                    Subtree = GetSubtreeTopDown(folder, childrenByParent)
                })
                .ToList();

            var allFolders = subtrees.SelectMany(entry => entry.Subtree).ToList();
            var allFolderIds = allFolders.Select(f => f.Id).ToList();

            var documents = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => allFolderIds.Contains(f.FolderId)))
                .ToList();

            if (documents.Count > 0)
            {
                var occupiedAreas = subtrees
                    .Where(entry => entry.Subtree.Any(folder => documents.Any(d => d.FolderId == folder.Id)))
                    .Select(entry => entry.Folder.Area)
                    .Distinct()
                    .OrderBy(area => area)
                    .Select(area => area.ToString());

                throw new ApiExceptionResponse(
                    $"Folder still contains {documents.Count} document(s) in zone(s): "
                    + string.Join(", ", occupiedAreas) + ".", 409);
            }

            foreach (var folder in Enumerable.Reverse(allFolders))
                _unitOfWork.Repository<Folder>().Delete(folder);

            var subFolderCount = allFolders.Count - mirrorGroup.Count;
            var subFolderNote = subFolderCount > 0 ? $" cùng {subFolderCount} thư mục con" : string.Empty;
            await _auditLog.LogAsync(
                LogScope.Group, AuditAction.Delete, nameof(Folder), entity.Id.ToString(), actorId,
                detail: $"Xoá thư mục '{entity.Name}' ở {mirrorGroup.Count} khu vực{subFolderNote}",
                projectId: entity.ProjectId, folderId: entity.Id);

            await _unitOfWork.CommitAsync();
        }

        private async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId)
            => (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ProjectId == projectId && !f.IsTemplate))
                .ToList();

        private static List<Folder> ResolveMirrorGroup(Folder folder, List<Folder> projectFolders)
        {
            var source = folder.MirrorSourceFolderId.HasValue
                ? projectFolders.FirstOrDefault(f => f.Id == folder.MirrorSourceFolderId.Value) ?? folder
                : folder;

            var group = new List<Folder> { source };
            group.AddRange(projectFolders.Where(f => f.MirrorSourceFolderId == source.Id && f.Id != source.Id));

            if (group.All(f => f.Id != folder.Id))
                group.Add(folder);

            return group;
        }

        private static void EnsureNameAvailable(string name, List<Folder> targets, List<Folder> projectFolders)
        {
            var targetIds = targets.Select(f => f.Id).ToHashSet();

            foreach (var target in targets)
            {
                var clash = projectFolders.FirstOrDefault(f =>
                    !targetIds.Contains(f.Id)
                    && f.Area == target.Area
                    && f.ParentFolderId == target.ParentFolderId
                    && string.Equals(f.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));

                if (clash != null)
                    throw new ApiExceptionResponse(
                        $"Folder name '{name}' already exists in zone: {clash.Area}.", 409);
            }
        }

        private static Dictionary<Guid, List<Folder>> GroupByParent(List<Folder> projectFolders)
            => projectFolders
                .Where(f => f.ParentFolderId.HasValue)
                .GroupBy(f => f.ParentFolderId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

        private static List<Folder> GetSubtreeTopDown(Folder root, Dictionary<Guid, List<Folder>> childrenByParent)
        {
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
