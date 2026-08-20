using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.RequestDTOs.ZoneReturn;
using Application.DTOs.ResponseDTOs.ZoneReturn;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Application.Services
{
    public class ZoneReturnRequestService : IZoneReturnRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileZoneResolverService _zoneResolver;
        private readonly IFileVersionService _fileVersionService;
        private readonly IAuditLogService _auditLog;
        private readonly IFileArchiveService _fileArchive;
        private readonly IDocumentIndexSyncService _indexSync;

        public ZoneReturnRequestService(
            IUnitOfWork unitOfWork,
            IFileZoneResolverService zoneResolver,
            IFileVersionService fileVersionService,
            IAuditLogService auditLog,
            IFileArchiveService fileArchive,
            IDocumentIndexSyncService indexSync)
        {
            _unitOfWork = unitOfWork;
            _zoneResolver = zoneResolver;
            _fileVersionService = fileVersionService;
            _auditLog = auditLog;
            _fileArchive = fileArchive;
            _indexSync = indexSync;
        }

        public async Task<ApiResponse> CreateAsync(Guid fileItemId, CreateZoneReturnRequestDTO dto, Guid actorId)
        {
            var reason = dto.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                throw new ApiExceptionResponse("Reason is required.", 400);

            var fileItem = await GetFileItemAsync(fileItemId);
            var currentFolder = await GetFolderAsync(fileItem.FolderId);

            if (currentFolder.Area == CdeArea.Wip)
                throw new ApiExceptionResponse("File in WIP cannot create return request.", 400);

            if (fileItem.Status == FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("File is pending approval and cannot create return request.", 400);

            var hasPendingRequest = (await _unitOfWork.Repository<ZoneReturnRequest>().FindAsync(
                    r => r.FileItemId == fileItem.Id && r.Status == ZoneReturnRequestStatus.Pending))
                .Any();
            if (hasPendingRequest)
                throw new ApiExceptionResponse("File already has a pending return request.", 400);

            var returnRequest = new ZoneReturnRequest
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItem.Id,
                IssueId = dto.IssueId,
                FromZone = currentFolder.Area,
                TargetZone = CdeArea.Wip,
                RequestedBy = actorId,
                Status = ZoneReturnRequestStatus.Pending,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<ZoneReturnRequest>().CreateAsync(returnRequest);

            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.ReturnRequest, nameof(ZoneReturnRequest),
                returnRequest.Id.ToString(), actorId,
                detail: $"Yêu cầu trả '{fileItem.Name}' về WIP (từ {currentFolder.Area}) — lý do: {reason}",
                projectId: currentFolder.ProjectId, folderId: currentFolder.Id);

            await _unitOfWork.CommitAsync();

            return ApiResponse.Success("Return request created", new CreateZoneReturnRequestResponseDTO
            {
                ReturnRequestId = returnRequest.Id,
                FileId = fileItem.Id,
                FromZone = _zoneResolver.FormatZone(returnRequest.FromZone),
                TargetZone = _zoneResolver.FormatZone(returnRequest.TargetZone),
                Status = returnRequest.Status.ToString()
            });
        }

        public async Task<ApiResponse> GetPendingAsync(Guid actorId)
        {
            var leaderGroupIds = await _zoneResolver.GetActiveLeaderGroupIdsAsync(actorId);
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

                var projectFolders = await _zoneResolver.GetProjectFoldersAsync(currentFolder.ProjectId);
                var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);
                if (!teamGroupIds.Any(leaderGroupIds.Contains))
                    continue;

                result.Add(new ZoneReturnRequestResponseDTO
                {
                    ReturnRequestId = request.Id,
                    FileId = fileItem.Id,
                    FileName = fileItem.Name,
                    FromZone = _zoneResolver.FormatZone(request.FromZone),
                    TargetZone = _zoneResolver.FormatZone(request.TargetZone),
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

            RequirePendingRequest(request);
            await RequireActiveTeamLeaderAsync(actorId, fileItem, currentFolder);

            if (currentFolder.Area != request.FromZone)
                throw new ApiExceptionResponse("File is no longer in the requested source zone.", 400);

            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(currentFolder.ProjectId);
            var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);
            var wipFolder = await _zoneResolver.ResolveTargetFolderAsync(
                currentFolder,
                CdeArea.Wip,
                teamGroupIds,
                projectFolders,
                "Target WIP folder not found.");

            // Niêm phong TRƯỚC khi rút file khỏi Published: sau khi re-parent thì folder không còn ở
            // Published (SealForZoneReturnAsync bỏ qua) và CurrentVersionId cũng đã bị đổi sang bản WIP
            // ở dưới. Không chốt lại ở đây thì bản chính thức biến mất khỏi tra cứu ngữ nghĩa.
            // Trả từ Shared, hoặc bản này đã niêm phong rồi -> trả null, luồng trả vùng chạy tiếp bình thường.
            var archivedMirrorId = await _fileArchive.SealForZoneReturnAsync(fileItem.Id, actorId);

            var now = DateTime.UtcNow;
            fileItem.FolderId = wipFolder.Id;
            fileItem.Status = FileItemStatus.Draft;
            fileItem.IsSigned = false;
            fileItem.UpdatedAt = now;

            request.Status = ZoneReturnRequestStatus.Approved;
            request.ApprovedBy = actorId;
            request.DecidedAt = now;

            // File trả về WIP -> thu hồi toàn bộ grant xem theo tài khoản (người được assign ký ở
            // vòng Shared->Published trước đó). Vô điều kiện: nếu nhóm họ vẫn có CanView thì vẫn xem
            // được qua ACL nhóm; nếu không thì mất quyền xem đúng như mong muốn.
            var viewGrants = (await _unitOfWork.Repository<FileViewGrant>().FindAsync(
                    g => g.FileItemId == fileItem.Id))
                .ToList();
            foreach (var grant in viewGrants)
                _unitOfWork.Repository<FileViewGrant>().Delete(grant);

            // Versioning: quay về WIP từ tài liệu đã publish (C{rev}) -> P{WorkingRevision}.01,
            // PublishedRevision bảo toàn. Quay về từ Shared: version giữ nguyên.
            if (fileItem.CurrentVersionId.HasValue)
            {
                var currentVersion = await _fileVersionService.GetCurrentVersionAsync(fileItem.Id);
                if (currentVersion?.Stage == VersionStage.Published)
                {
                    var result = await _fileVersionService.GetReturnToWipVersionAsync(fileItem.Id);
                    fileItem.CurrentVersionId = result.VersionStateId;
                }
            }

            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Approve, nameof(ZoneReturnRequest), request.Id.ToString(), actorId,
                detail: $"Duyệt trả '{fileItem.Name}' về WIP (từ {request.FromZone})",
                projectId: currentFolder.ProjectId, folderId: currentFolder.Id);

            await _unitOfWork.CommitAsync();

            // Bản lưu vừa niêm phong cần vào chỉ mục để tìm kiếm rơi về được bản chính thức gần nhất
            // trong lúc bản gốc đang được sửa ở WIP (nhãn IsUnderRevision phía FE).
            if (archivedMirrorId.HasValue)
                await _indexSync.RequestIndexAsync(archivedMirrorId.Value);

            return ApiResponse.Success("Return request approved", new ZoneReturnDecisionResponseDTO
            {
                ReturnRequestId = request.Id,
                FileId = fileItem.Id,
                FromZone = _zoneResolver.FormatZone(request.FromZone),
                ToZone = _zoneResolver.FormatZone(CdeArea.Wip),
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

            RequirePendingRequest(request);
            await RequireActiveTeamLeaderAsync(actorId, fileItem, currentFolder);

            var now = DateTime.UtcNow;
            request.Status = ZoneReturnRequestStatus.Rejected;
            request.ApprovedBy = actorId;
            request.DecidedAt = now;
            request.RejectReason = rejectReason;

            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Reject, nameof(ZoneReturnRequest), request.Id.ToString(), actorId,
                detail: $"Từ chối trả '{fileItem.Name}' về WIP — lý do: {rejectReason}",
                projectId: currentFolder.ProjectId, folderId: currentFolder.Id);

            await _unitOfWork.CommitAsync();

            return ApiResponse.Success("Return to WIP request rejected. File remains in current zone.", new ZoneReturnDecisionResponseDTO
            {
                ReturnRequestId = request.Id,
                FileId = fileItem.Id,
                FromZone = _zoneResolver.FormatZone(request.FromZone),
                ToZone = _zoneResolver.FormatZone(request.TargetZone),
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

        private static void RequirePendingRequest(ZoneReturnRequest request)
        {
            if (request.Status != ZoneReturnRequestStatus.Pending)
                throw new ApiExceptionResponse("Only pending return requests can be approved or rejected.", 400);
        }

        private async Task RequireActiveTeamLeaderAsync(Guid actorId, FileItem fileItem, Folder currentFolder)
        {
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(currentFolder.ProjectId);
            var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(fileItem, currentFolder, projectFolders);

            await _zoneResolver.RequireActiveTeamLeaderAsync(
                actorId,
                teamGroupIds,
                "Only the active Team Leader can decide this return request.");
        }
    }
}
