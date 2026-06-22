using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.ZoneReturn;
using Application.DTOs.ResponseDTOs.ZoneReturn;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Group;
using Domain.Enum.Project;

namespace Application.Services
{
    public class ZoneReturnRequestService : IZoneReturnRequestService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ZoneReturnRequestService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse> GetPendingAsync(Guid actorId)
        {
            var leaderGroupIds = await GetActiveLeaderGroupIdsAsync(actorId);
            if (leaderGroupIds.Count == 0)
                throw new ApiExceptionResponse("Only active Team Leader can view pending return requests.", 403);

            var requests = (await _unitOfWork.Repository<ZoneReturnRequest>().FindAsync(
                    r => r.Status == ZoneReturnRequestStatus.Pending))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            var result = new List<ZoneReturnRequestResponseDTO>();
            foreach (var request in requests)
            {
                var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(request.FileItemId);
                if (fileItem == null)
                    continue;

                var currentFolder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);
                if (currentFolder == null)
                    continue;

                var projectFolders = await GetProjectFoldersAsync(currentFolder.ProjectId);
                var teamGroupIds = await ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);
                if (!teamGroupIds.Any(leaderGroupIds.Contains))
                    continue;

                result.Add(new ZoneReturnRequestResponseDTO
                {
                    ReturnRequestId = request.Id,
                    FileId = fileItem.Id,
                    FileName = fileItem.Name,
                    FromZone = FormatZone(request.FromZone),
                    TargetZone = FormatZone(request.TargetZone),
                    RequestedBy = request.RequestedBy,
                    Reason = request.Reason,
                    CreatedAt = request.CreatedAt,
                    Status = request.Status.ToString()
                });
            }

            return ApiResponse.Success("Pending return requests retrieved", result);
        }

        public async Task<ApiResponse> ApproveAsync(Guid requestId, Guid actorId)
        {
            var request = await GetRequestAsync(requestId);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            var currentFolder = await GetFolderAsync(fileItem.FolderId);

            await RequirePendingRequestAsync(request);
            await RequireActiveTeamLeaderAsync(actorId, fileItem, currentFolder);

            if (currentFolder.Area != request.FromZone)
                throw new ApiExceptionResponse("File is no longer in the requested source zone.", 400);

            var projectFolders = await GetProjectFoldersAsync(currentFolder.ProjectId);
            var teamGroupIds = await ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);
            var wipFolder = await ResolveTargetFolderAsync(currentFolder, CdeArea.Wip, teamGroupIds, projectFolders);

            var now = DateTime.UtcNow;
            fileItem.FolderId = wipFolder.Id;
            fileItem.Status = FileItemStatus.Draft;
            fileItem.IsSigned = false;
            fileItem.UpdatedAt = now;

            request.Status = ZoneReturnRequestStatus.Approved;
            request.ApprovedBy = actorId;
            request.DecidedAt = now;

            await _unitOfWork.CommitAsync();

            return ApiResponse.Success("Return request approved", new ZoneReturnDecisionResponseDTO
            {
                ReturnRequestId = request.Id,
                FileId = fileItem.Id,
                FromZone = FormatZone(request.FromZone),
                ToZone = FormatZone(CdeArea.Wip),
                Status = request.Status.ToString()
            });
        }

        public async Task<ApiResponse> RejectAsync(Guid requestId, RejectZoneReturnRequestDTO dto, Guid actorId)
        {
            var rejectReason = dto.RejectReason?.Trim();
            if (string.IsNullOrWhiteSpace(rejectReason))
                throw new ApiExceptionResponse("Reject reason is required.", 400);

            var request = await GetRequestAsync(requestId);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            var currentFolder = await GetFolderAsync(fileItem.FolderId);

            await RequirePendingRequestAsync(request);
            await RequireActiveTeamLeaderAsync(actorId, fileItem, currentFolder);

            var now = DateTime.UtcNow;
            request.Status = ZoneReturnRequestStatus.Rejected;
            request.ApprovedBy = actorId;
            request.DecidedAt = now;
            request.RejectReason = rejectReason;

            await _unitOfWork.CommitAsync();

            return ApiResponse.Success("Return request rejected", new ZoneReturnDecisionResponseDTO
            {
                ReturnRequestId = request.Id,
                FileId = fileItem.Id,
                Status = request.Status.ToString(),
                RejectReason = request.RejectReason
            });
        }

        private async Task<ZoneReturnRequest> GetRequestAsync(Guid requestId)
            => await _unitOfWork.Repository<ZoneReturnRequest>().GetByIdAsync(requestId)
               ?? throw new ApiExceptionResponse("Return request not found.", 404);

        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        private async Task<Folder> GetFolderAsync(Guid folderId)
            => await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
               ?? throw new ApiExceptionResponse("File folder not found.", 404);

        private static Task RequirePendingRequestAsync(ZoneReturnRequest request)
        {
            if (request.Status != ZoneReturnRequestStatus.Pending)
                throw new ApiExceptionResponse("Only pending return requests can be approved or rejected.", 400);

            return Task.CompletedTask;
        }

        private async Task<HashSet<Guid>> GetActiveLeaderGroupIdsAsync(Guid actorId)
            => (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => m.AccountId == actorId
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Select(m => m.GroupId)
                .ToHashSet();

        private static string FormatZone(CdeArea zone)
            => zone == CdeArea.Wip ? "WIP" : zone.ToString();

        private async Task RequireActiveTeamLeaderAsync(Guid actorId, FileItem fileItem, Folder currentFolder)
        {
            var projectFolders = await GetProjectFoldersAsync(currentFolder.ProjectId);
            var teamGroupIds = await ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);
            var leaderGroupIds = await GetActiveLeaderGroupIdsAsync(actorId);

            if (!teamGroupIds.Any(leaderGroupIds.Contains))
                throw new ApiExceptionResponse("Only the active Team Leader can decide this return request.", 403);
        }

        private async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId)
            => (await _unitOfWork.Repository<Folder>().FindAsync(
                    f => f.ProjectId == projectId && !f.IsTemplate))
                .ToList();

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

            throw new ApiExceptionResponse("Target WIP folder not found.", 404);
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
    }
}
