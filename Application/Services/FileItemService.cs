using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Group;
using Domain.Enum.Project;

namespace Application.Services
{
    public class FileItemService : IFileItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderPermissionService _permission;
        private readonly IMapper _mapper;

        public FileItemService(
            IUnitOfWork unitOfWork,
            IFolderPermissionService permission,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _permission = permission;
            _mapper = mapper;
        }

        public async Task<IEnumerable<FileItemResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<FileItemResponseDTO>>(
                await _unitOfWork.Repository<FileItem>().GetAllAsync());

        public async Task<FileItemResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<FileItem>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<FileItemResponseDTO>(entity);
        }

        public async Task<FileItemResponseDTO> CreateAsync(CreateFileItemDTO dto)
        {
            var entity = _mapper.Map<FileItem>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<FileItem>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FileItemResponseDTO>(entity);
        }

        public async Task<FileItemResponseDTO> UpdateAsync(Guid id, UpdateFileItemDTO dto)
        {
            var entity = await _unitOfWork.Repository<FileItem>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"FileItem with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<FileItem>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FileItemResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<FileItem>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"FileItem with ID {id} not found.", 404);
            _unitOfWork.Repository<FileItem>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }

        public async Task<TransferZoneResponseDTO> TransferZoneAsync(Guid fileItemId, TransferZoneRequestDTO dto, Guid actorId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            var currentFolder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            var targetZone = ParseTargetZone(dto.TargetZone);
            var currentZone = currentFolder.Area;

            ValidateTransferRules(fileItem, currentZone, targetZone);

            var projectFolders = (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ProjectId == currentFolder.ProjectId && !f.IsTemplate))
                .ToList();
            var teamGroupIds = await ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);

            await RequireActiveTeamLeaderAsync(actorId, teamGroupIds);

            var targetFolder = await ResolveTargetFolderAsync(
                currentFolder,
                targetZone,
                teamGroupIds,
                projectFolders);

            var fromZone = FormatZone(currentZone);
            fileItem.FolderId = targetFolder.Id;
            fileItem.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            return new TransferZoneResponseDTO
            {
                FileId = fileItem.Id,
                FromZone = fromZone,
                ToZone = FormatZone(targetZone),
                FolderId = targetFolder.Id,
                Message = $"File transferred from {fromZone} to {FormatZone(targetZone)}."
            };
        }

        // Danh sách file trong 1 folder (gộp version hiện hành + tác giả). Gate quyền View.
        public async Task<IEnumerable<FileListItemDTO>> GetByFolderAsync(Guid folderId, Guid actorId)
        {
            _ = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            await _permission.RequireAsync(actorId, folderId, FolderAction.View);

            var files = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => f.FolderId == folderId))
                .ToList();
            if (files.Count == 0) return Enumerable.Empty<FileListItemDTO>();

            var fileIds = files.Select(f => f.Id).ToList();
            var versionsById = (await _unitOfWork.Repository<FileVersion>()
                    .FindAsync(v => fileIds.Contains(v.FileItemId)))
                .ToDictionary(v => v.Id);
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return files.Select(f =>
            {
                FileVersion? cur = f.CurrentVersionId.HasValue && versionsById.TryGetValue(f.CurrentVersionId.Value, out var v) ? v : null;
                return new FileListItemDTO
                {
                    Id = f.Id,
                    FolderId = f.FolderId,
                    Name = f.Name,
                    FileType = f.FileType,
                    Status = f.Status,
                    CurrentVersionId = f.CurrentVersionId,
                    CurrentVersionNumber = cur?.VersionNumber ?? 0,
                    SizeBytes = cur?.FileSizeBytes ?? 0,
                    Format = cur?.Format,
                    CreatedByAccountId = f.CreatedByAccountId,
                    AuthorName = f.CreatedByAccountId.HasValue && accounts.TryGetValue(f.CreatedByAccountId.Value, out var a) ? a.UserName : null,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt,
                };
            }).ToList();
        }

        // Tất cả phiên bản của 1 file (mới nhất trước). Gate quyền View trên folder của file.
        public async Task<IEnumerable<FileVersionResponseDTO>> GetVersionsAsync(Guid fileItemId, Guid actorId)
        {
            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            await _permission.RequireAsync(actorId, file.FolderId, FolderAction.View);

            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return (await _unitOfWork.Repository<FileVersion>()
                    .FindAsync(v => v.FileItemId == fileItemId))
                .OrderByDescending(v => v.VersionNumber)
                .Select(v =>
                {
                    var dto = _mapper.Map<FileVersionResponseDTO>(v);
                    dto.UploadedByName = v.UploadedByAccountId.HasValue && accounts.TryGetValue(v.UploadedByAccountId.Value, out var a) ? a.UserName : null;
                    return dto;
                })
                .ToList();
        }
    }
}

        private static CdeArea ParseTargetZone(string? targetZone)
        {
            if (string.IsNullOrWhiteSpace(targetZone)
                || !Enum.TryParse<CdeArea>(targetZone.Trim(), ignoreCase: true, out var parsed))
                throw new ApiExceptionResponse("Invalid target zone.", 400);

            return parsed;
        }

        private static void ValidateTransferRules(FileItem fileItem, CdeArea currentZone, CdeArea targetZone)
        {
            if (fileItem.Status == FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("File is pending approval and cannot be transferred.", 400);

            if (currentZone == targetZone)
                throw new ApiExceptionResponse("File cannot be transferred to the same zone.", 400);

            if (!IsAllowedTransition(currentZone, targetZone))
                throw new ApiExceptionResponse($"Invalid zone transition: {currentZone} -> {targetZone}.", 400);

            if (currentZone == CdeArea.Wip && targetZone == CdeArea.Shared)
            {
                if (fileItem.Status != FileItemStatus.Approved)
                    throw new ApiExceptionResponse("File must be approved before transferring from WIP to Shared.", 400);

                if (fileItem.RequiresSignature && !fileItem.IsSigned)
                    throw new ApiExceptionResponse("Document requires signature before transfer.", 400);
            }
        }

        private static bool IsAllowedTransition(CdeArea currentZone, CdeArea targetZone)
            => (currentZone, targetZone) is
                (CdeArea.Wip, CdeArea.Shared)
                or (CdeArea.Shared, CdeArea.Published)
                or (CdeArea.Published, CdeArea.Archived)
                or (CdeArea.Shared, CdeArea.Wip)
                or (CdeArea.Published, CdeArea.Wip)
                or (CdeArea.Archived, CdeArea.Wip);

        private static string FormatZone(CdeArea zone)
            => zone == CdeArea.Wip ? "WIP" : zone.ToString();

        private async Task<IReadOnlyCollection<Guid>> ResolveFileTeamGroupIdsAsync(
            FileItem fileItem,
            Folder currentFolder,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var activeParticipants = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == currentFolder.ProjectId && p.Status == ProjectParticipantStatus.Active))
                .ToDictionary(p => p.Id, p => p.GroupId);
            if (activeParticipants.Count == 0)
                throw new ApiExceptionResponse("File project has no active team.", 400);

            var teamGroupIds = new HashSet<Guid>();

            var filePermissions = await _unitOfWork.Repository<FilePermission>().FindAsync(
                p => p.FileItemId == fileItem.Id && p.ProjectParticipantId.HasValue);
            foreach (var permission in filePermissions)
            {
                if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
                    teamGroupIds.Add(groupId);
            }

            var folderIds = ResolveFolderPathIds(currentFolder, projectFolders);
            var folderPermissions = await _unitOfWork.Repository<FolderPermission>().FindAsync(
                p => folderIds.Contains(p.FolderId) && p.ProjectParticipantId.HasValue);
            foreach (var permission in folderPermissions)
            {
                if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
                    teamGroupIds.Add(groupId);
            }

            return teamGroupIds.Count > 0
                ? teamGroupIds
                : activeParticipants.Values.ToHashSet();
        }

        private async Task RequireActiveTeamLeaderAsync(Guid actorId, IReadOnlyCollection<Guid> teamGroupIds)
        {
            var isLeader = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => teamGroupIds.Contains(m.GroupId)
                         && m.AccountId == actorId
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Any();

            if (!isLeader)
                throw new ApiExceptionResponse("Only the active Team Leader can transfer this file.", 403);
        }

        private async Task<Folder> ResolveTargetFolderAsync(
            Folder currentFolder,
            CdeArea targetZone,
            IReadOnlyCollection<Guid> teamGroupIds,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var participantIds = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == currentFolder.ProjectId
                         && p.Status == ProjectParticipantStatus.Active
                         && teamGroupIds.Contains(p.GroupId)))
                .Select(p => p.Id)
                .ToHashSet();

            var targetFolders = projectFolders
                .Where(f => f.Area == targetZone)
                .ToDictionary(f => f.Id);

            var permissionTargetFolder = await FindTargetFolderByPermissionAsync(targetFolders, participantIds);
            if (permissionTargetFolder != null)
                return permissionTargetFolder;

            var sameTeamFolder = await FindTargetFolderByTeamNameAsync(
                currentFolder,
                targetZone,
                teamGroupIds,
                projectFolders);
            if (sameTeamFolder != null)
                return sameTeamFolder;

            throw new ApiExceptionResponse("Target folder not found.", 404);
        }

        private async Task<Folder?> FindTargetFolderByPermissionAsync(
            IReadOnlyDictionary<Guid, Folder> targetFolders,
            IReadOnlyCollection<Guid> participantIds)
        {
            if (participantIds.Count == 0 || targetFolders.Count == 0)
                return null;

            var targetFolderIds = targetFolders.Keys.ToHashSet();
            var folderPermission = (await _unitOfWork.Repository<FolderPermission>().FindAsync(
                    p => targetFolderIds.Contains(p.FolderId)
                         && p.ProjectParticipantId.HasValue
                         && participantIds.Contains(p.ProjectParticipantId.Value)))
                .FirstOrDefault();

            return folderPermission != null && targetFolders.TryGetValue(folderPermission.FolderId, out var folder)
                ? folder
                : null;
        }

        private async Task<Folder?> FindTargetFolderByTeamNameAsync(
            Folder currentFolder,
            CdeArea targetZone,
            IReadOnlyCollection<Guid> teamGroupIds,
            IReadOnlyCollection<Folder> projectFolders)
        {
            var targetRoot = projectFolders.FirstOrDefault(f => f.ParentFolderId == null && f.Area == targetZone);
            if (targetRoot == null)
                return null;

            var currentTeamFolder = ResolveZoneTeamFolder(currentFolder, projectFolders);
            if (currentTeamFolder != null)
            {
                var matchingFolder = projectFolders.FirstOrDefault(
                    f => f.ParentFolderId == targetRoot.Id
                         && string.Equals(f.Name, currentTeamFolder.Name, StringComparison.OrdinalIgnoreCase));
                if (matchingFolder != null)
                    return matchingFolder;
            }

            var teamNames = (await _unitOfWork.Repository<Group>().FindAsync(g => teamGroupIds.Contains(g.Id)))
                .Select(g => g.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return projectFolders.FirstOrDefault(
                f => f.ParentFolderId == targetRoot.Id && teamNames.Contains(f.Name));
        }

        private static HashSet<Guid> ResolveFolderPathIds(Folder folder, IReadOnlyCollection<Folder> projectFolders)
        {
            var byId = projectFolders.ToDictionary(f => f.Id);
            var folderIds = new HashSet<Guid>();
            var current = folder;

            while (folderIds.Add(current.Id)
                   && current.ParentFolderId.HasValue
                   && byId.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                current = parent;
            }

            return folderIds;
        }

        private static Folder? ResolveZoneTeamFolder(Folder folder, IReadOnlyCollection<Folder> projectFolders)
        {
            var byId = projectFolders.ToDictionary(f => f.Id);
            var current = folder;
            Folder? teamFolder = null;

            while (current.ParentFolderId.HasValue
                   && byId.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                teamFolder = current;
                current = parent;
            }

            return current.ParentFolderId == null ? teamFolder : null;
        }
