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

namespace Application.Services
{
    public class FileItemService : IFileItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderPermissionService _permission;
        private readonly IFileZoneResolverService _zoneResolver;
        private readonly IMapper _mapper;

        public FileItemService(
            IUnitOfWork unitOfWork,
            IFolderPermissionService permission,
            IFileZoneResolverService zoneResolver,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _permission = permission;
            _zoneResolver = zoneResolver;
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
            var fileItem = await GetFileItemAsync(fileItemId);
            var currentFolder = await GetFolderAsync(fileItem.FolderId);
            var targetZone = ParseTargetZone(dto.TargetZone);

            ValidateTransferRules(fileItem, currentFolder.Area, targetZone);

            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(currentFolder.ProjectId);
            var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);

            await _zoneResolver.RequireActiveTeamLeaderAsync(
                actorId,
                teamGroupIds,
                "Only the active Team Leader can transfer this file.");

            var targetFolder = await _zoneResolver.ResolveTargetFolderAsync(
                currentFolder,
                targetZone,
                teamGroupIds,
                projectFolders,
                "Target folder not found.");

            var now = DateTime.UtcNow;
            var fromZone = _zoneResolver.FormatZone(currentFolder.Area);

            fileItem.FolderId = targetFolder.Id;
            fileItem.UpdatedAt = now;

            await _unitOfWork.CommitAsync();

            return new TransferZoneResponseDTO
            {
                FileId = fileItem.Id,
                FromZone = fromZone,
                ToZone = _zoneResolver.FormatZone(targetZone),
                FolderId = targetFolder.Id,
                Message = $"File transferred from {fromZone} to {_zoneResolver.FormatZone(targetZone)}."
            };
        }

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
            var returnRequestsByFileId = (await _unitOfWork.Repository<ZoneReturnRequest>().FindAsync(
                    r => fileIds.Contains(r.FileItemId)
                         && (r.Status == ZoneReturnRequestStatus.Pending
                             || r.Status == ZoneReturnRequestStatus.Rejected)))
                .GroupBy(r => r.FileItemId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.DecidedAt ?? r.CreatedAt).ToList());

            return files.Select(f =>
            {
                FileVersion? cur = f.CurrentVersionId.HasValue && versionsById.TryGetValue(f.CurrentVersionId.Value, out var v) ? v : null;
                var returnRequest = ResolveVisibleReturnRequest(f, returnRequestsByFileId);
                return new FileListItemDTO
                {
                    Id = f.Id,
                    FolderId = f.FolderId,
                    Name = f.Name,
                    FileType = f.FileType,
                    Status = f.Status,
                    ReturnRequestStatus = returnRequest?.Status,
                    ReturnTargetZone = returnRequest == null ? null : _zoneResolver.FormatZone(returnRequest.TargetZone),
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

        private static ZoneReturnRequest? ResolveVisibleReturnRequest(
            FileItem fileItem,
            IReadOnlyDictionary<Guid, List<ZoneReturnRequest>> requestsByFileId)
        {
            if (!requestsByFileId.TryGetValue(fileItem.Id, out var requests))
                return null;

            return requests.FirstOrDefault(request =>
                request.Status == ZoneReturnRequestStatus.Pending
                || !fileItem.UpdatedAt.HasValue
                || (request.DecidedAt ?? request.CreatedAt) >= fileItem.UpdatedAt.Value);
        }

        public async Task<IEnumerable<FileVersionResponseDTO>> GetVersionsAsync(Guid fileItemId, Guid actorId)
        {
            var file = await GetFileItemAsync(fileItemId);
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

        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        private async Task<Folder> GetFolderAsync(Guid folderId)
            => await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
               ?? throw new ApiExceptionResponse("File folder not found.", 404);

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

            if (fileItem.Status == FileItemStatus.Rejected)
                throw new ApiExceptionResponse("Rejected file cannot be transferred.", 400);

            if (currentZone == targetZone)
                throw new ApiExceptionResponse("File cannot be transferred to the same zone.", 400);

            if (!IsAllowedTransition(currentZone, targetZone))
                throw new ApiExceptionResponse($"Invalid zone transition: {currentZone} -> {targetZone}.", 400);

            if (IsForwardApprovalTransition(currentZone, targetZone))
                throw new ApiExceptionResponse("Forward zone transfer requires an approval request.", 400);

            if (targetZone == CdeArea.Wip && fileItem.Status != FileItemStatus.Approved)
                throw new ApiExceptionResponse("Only approved files can be returned to WIP.", 400);
        }

        private static bool IsAllowedTransition(CdeArea currentZone, CdeArea targetZone)
            => (currentZone, targetZone) is
                (CdeArea.Wip, CdeArea.Shared)
                or (CdeArea.Shared, CdeArea.Published)
                or (CdeArea.Published, CdeArea.Archived)
                or (CdeArea.Shared, CdeArea.Wip)
                or (CdeArea.Published, CdeArea.Wip)
                or (CdeArea.Archived, CdeArea.Wip);

        private static bool IsForwardApprovalTransition(CdeArea currentZone, CdeArea targetZone)
            => (currentZone, targetZone) is
                (CdeArea.Wip, CdeArea.Shared)
                or (CdeArea.Shared, CdeArea.Published)
                or (CdeArea.Published, CdeArea.Archived);
    }
}
