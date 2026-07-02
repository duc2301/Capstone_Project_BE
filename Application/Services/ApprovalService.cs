using Application.DTOs.RequestDTOs.Approval;
using Application.DTOs.ResponseDTOs.Approval;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Group;
using Domain.Enum.Project;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// Xử lý nghiệp vụ phê duyệt file trong hệ thống CDE.
    /// </summary>
    public class ApprovalService : IApprovalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileZoneResolverService _zoneResolver;
        private readonly ILogger<ApprovalService> _logger;

        public ApprovalService(
            IUnitOfWork unitOfWork,
            IFileZoneResolverService zoneResolver,
            ILogger<ApprovalService> logger)
        {
            _unitOfWork = unitOfWork;
            _zoneResolver = zoneResolver;
            _logger = logger;
        }

        #region API chính

        /// <summary>
        /// Gửi yêu cầu phê duyệt file. Chỉ member active trong team của file mới được thực hiện.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> SubmitAsync(
            Guid fileItemId,
            SubmitApprovalRequestDTO? dto,
            Guid actor)
        {
            var fileItem = await GetFileItemAsync(fileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);

            RequireCanSubmitApproval(fileItem, folder.Area);

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(
                fileItem,
                folder,
                requireApprovePermission: false);

            await RequireGroupMemberAsync(actor, teamGroupIds);

            var hasPendingRequest = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.FileItemId == fileItem.Id && a.Status == ApprovalRequestStatus.Pending))
                .Any();
            if (hasPendingRequest)
                throw new ApiExceptionResponse("File is already pending approval.", 409);

            var hasPendingReturnRequest = (await _unitOfWork.Repository<ZoneReturnRequest>().FindAsync(
                    r => r.FileItemId == fileItem.Id && r.Status == ZoneReturnRequestStatus.Pending))
                .Any();
            if (hasPendingReturnRequest)
                throw new ApiExceptionResponse("File has a pending return to WIP request.", 409);

            var now = DateTime.UtcNow;
            var request = new ApprovalRequest
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItem.Id,
                RequestedBy = actor,
                Status = ApprovalRequestStatus.Pending,
                CreatedAt = now
            };

            fileItem.RequiresSignature = dto?.RequiresSignature ?? false;
            fileItem.IsSigned = false;
            fileItem.Status = FileItemStatus.PendingApproval;
            fileItem.UpdatedAt = now;

            await _unitOfWork.Repository<ApprovalRequest>().CreateAsync(request);
            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(request, fileItem);
        }

        /// <summary>
        /// Lấy tất cả approval request mà actor có quyền xem.
        /// </summary>
        public async Task<IEnumerable<ApprovalRequestResponseDTO>> GetAllAsync(Guid actor)
        {
            var requests = (await _unitOfWork.Repository<ApprovalRequest>().GetAllAsync())
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return await FilterVisibleRequestsAsync(requests, actor);
        }

        /// <summary>
        /// Lấy các approval request đang Pending mà actor có quyền xem.
        /// </summary>
        public async Task<IEnumerable<ApprovalRequestResponseDTO>> GetPendingAsync(Guid actor)
        {
            var pendingRequests = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.Status == ApprovalRequestStatus.Pending))
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return await FilterVisibleRequestsAsync(pendingRequests, actor);
        }

        /// <summary>
        /// Lấy chi tiết approval request theo id. Actor phải là người gửi hoặc Team Leader.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> GetByIdAsync(Guid id, Guid actor)
        {
            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);

            if (!await CanViewRequestAsync(actor, request, fileItem))
                throw new ApiExceptionResponse("You do not have permission to view this approval request.", 403);

            return await BuildResponseAsync(request, fileItem);
        }

        /// <summary>
        /// Duyệt approval request. Chỉ Team Leader mới được thực hiện.
        /// Nếu file yêu cầu ký số thì phải hoàn tất ký trước khi duyệt.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> ApproveAsync(Guid id, Guid actor)
        {
            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);

            await RequireCanDecideAsync(actor, request, fileItem, folder);
            await RequireSmartCaSignatureBeforeApprovalAsync(request, fileItem, folder);

            var now = DateTime.UtcNow;
            request.Status = ApprovalRequestStatus.Approved;
            request.ApproverId = actor;
            request.ApprovedAt = now;
            request.RejectReason = null;

            fileItem.Status = FileItemStatus.Approved;
            fileItem.UpdatedAt = now;

            await MoveApprovedFileToNextZoneAsync(fileItem, folder, now);

            await _unitOfWork.CommitAsync();
            return await BuildResponseAsync(request, fileItem);
        }

        /// <summary>
        /// Bắt buộc actor là Team Leader active của team phụ trách file (vd: trước khi đặt vị trí chữ ký
        /// hoặc sinh PDF đã ký) — không cần ApprovalRequest đang Pending như RequireCanDecideAsync.
        /// </summary>
        public async Task RequireTeamLeaderAsync(Guid fileItemId, Guid actorId)
        {
            var fileItem = await GetFileItemAsync(fileItemId);
            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: true);
            if (!await IsGroupLeaderAsync(actorId, teamGroupIds))
                throw new ApiExceptionResponse("Only the Team Leader can perform this action.", 403);
        }

        /// <summary>
        /// Từ chối approval request. Chỉ Team Leader mới được thực hiện. Bắt buộc có lý do.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> RejectAsync(Guid id, RejectApprovalRequestDTO dto, Guid actor)
        {
            var reason = dto.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                throw new ApiExceptionResponse("Reject reason is required.", 400);

            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);

            await RequireCanDecideAsync(actor, request, fileItem, folder);

            var now = DateTime.UtcNow;
            request.Status = ApprovalRequestStatus.Rejected;
            request.ApproverId = actor;
            request.ApprovedAt = now;
            request.RejectReason = reason;

            fileItem.Status = FileItemStatus.Rejected;
            fileItem.UpdatedAt = now;

            await _unitOfWork.CommitAsync();
            return await BuildResponseAsync(request, fileItem);
        }

        #endregion

        #region Lấy dữ liệu cơ bản

        /// <summary>Lấy FileItem theo id, ném 404 nếu không tìm thấy.</summary>
        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        /// <summary>Lấy ApprovalRequest theo id, ném 404 nếu không tìm thấy.</summary>
        private async Task<ApprovalRequest> GetRequestAsync(Guid id)
            => await _unitOfWork.Repository<ApprovalRequest>().GetByIdAsync(id)
               ?? throw new ApiExceptionResponse("Approval request not found.", 404);

        /// <summary>Lấy Folder theo id, ném 404 nếu không tìm thấy.</summary>
        private async Task<Folder> GetFolderAsync(Guid folderId)
            => await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
               ?? throw new ApiExceptionResponse("File folder not found.", 404);

        #endregion

        #region Kiểm tra quyền

        /// <summary>
        /// Yêu cầu actor là Team Leader có quyền approve file và request đang ở trạng thái Pending.
        /// </summary>
        private async Task RequireCanDecideAsync(Guid actor, ApprovalRequest request, FileItem fileItem, Folder folder)
        {
            if (request.Status != ApprovalRequestStatus.Pending || fileItem.Status != FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("Only pending approval requests can be approved or rejected.", 409);

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, folder, requireApprovePermission: true);
            if (!await IsGroupLeaderAsync(actor, teamGroupIds))
                throw new ApiExceptionResponse("Only the Team Leader can approve or reject this file.", 403);
        }

        /// <summary>
        /// File chỉ được gửi duyệt ở WIP, Shared hoặc Published.
        /// Sau khi đã approve qua một vùng, muốn đi tiếp phải gửi request duyệt mới.
        /// </summary>
        private static void RequireCanSubmitApproval(FileItem fileItem, CdeArea currentZone)
        {
            if (currentZone == CdeArea.Archived)
                throw new ApiExceptionResponse("Archived file cannot be submitted for approval.", 400);

            if (fileItem.Status == FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("File is already pending approval.", 409);

            if (fileItem.Status == FileItemStatus.Rejected)
                throw new ApiExceptionResponse("Rejected file cannot be submitted for approval.", 400);
        }

        /// <summary>
        /// Yêu cầu actor là member active trong team của file.
        /// </summary>
        private async Task RequireGroupMemberAsync(Guid accountId, IReadOnlyCollection<Guid> groupIds)
        {
            var isMember = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.AccountId == accountId
                         && m.Status == GroupMemberStatus.Active))
                .Any();
            if (!isMember)
                throw new ApiExceptionResponse("Only members of the file team can submit approval.", 403);
        }

        /// <summary>Kiểm tra actor có phải Group Leader active không.</summary>
        private async Task<bool> IsGroupLeaderAsync(Guid accountId, IReadOnlyCollection<Guid> groupIds)
            => (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.AccountId == accountId
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Any();

        /// <summary>
        /// Kiểm tra actor có quyền xem approval request không.
        /// Trả về true nếu actor là người gửi hoặc là Team Leader có quyền approve.
        /// </summary>
        private async Task<bool> CanViewRequestAsync(Guid actor, ApprovalRequest request, FileItem fileItem)
        {
            if (request.RequestedBy == actor)
                return true;

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: true);
            return await IsGroupLeaderAsync(actor, teamGroupIds);
        }

        /// <summary>
        /// Kiểm tra chữ ký số trước khi approve chuyển file ra khỏi WIP.
        /// Các bước Shared -> Published và Published -> Archived không yêu cầu ký lại.
        /// </summary>
        private async Task RequireSmartCaSignatureBeforeApprovalAsync(
            ApprovalRequest request,
            FileItem fileItem,
            Folder folder)
        {
            if (folder.Area != CdeArea.Wip || !fileItem.RequiresSignature)
                return;

            var hasSignedTransaction = (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == request.Id
                         && t.FileItemId == fileItem.Id
                         && t.Status == SignatureTransactionStatus.Signed))
                .Any();

            if (!fileItem.IsSigned || !fileItem.SignedVersionId.HasValue || !hasSignedTransaction)
                throw new ApiExceptionResponse(
                    "Signed PDF must be generated before approval.",
                    409);
        }

        /// <summary>
        /// Sau khi Team Leader approve thành công, file được chuyển sang vùng kế tiếp:
        /// WIP -> Shared, Shared -> Published, Published -> Archived.
        /// File bị reject không đi qua hàm này nên vẫn giữ nguyên folder hiện tại.
        /// </summary>
        private async Task MoveApprovedFileToNextZoneAsync(FileItem fileItem, Folder currentFolder, DateTime now)
        {
            var nextZone = GetNextApprovalZone(currentFolder.Area);
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(currentFolder.ProjectId);
            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(
                fileItem,
                currentFolder,
                requireApprovePermission: true);

            var targetFolder = await _zoneResolver.ResolveTargetFolderAsync(
                currentFolder,
                nextZone,
                teamGroupIds,
                projectFolders,
                $"{_zoneResolver.FormatZone(nextZone)} folder not found.");

            fileItem.FolderId = targetFolder.Id;
            fileItem.UpdatedAt = now;
        }

        private static CdeArea GetNextApprovalZone(CdeArea currentZone)
            => currentZone switch
            {
                CdeArea.Wip => CdeArea.Shared,
                CdeArea.Shared => CdeArea.Published,
                CdeArea.Published => CdeArea.Archived,
                _ => throw new ApiExceptionResponse("File is already archived and cannot move to next approval zone.", 400)
            };

        #endregion

        #region Xác định team của file

        /// <summary>
        /// Lấy danh sách GroupId của team phụ trách file dựa trên quyền file/folder.
        /// Fallback về toàn bộ group active trong project nếu không có permission nào được cấu hình.
        /// </summary>
        private async Task<IReadOnlyCollection<Guid>> ResolveFileItemTeamGroupIdsAsync(
            FileItem fileItem,
            Folder folder,
            bool requireApprovePermission)
        {
            var activeParticipants = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == folder.ProjectId && p.Status == ProjectParticipantStatus.Active))
                .ToDictionary(p => p.Id, p => p.GroupId);
            if (activeParticipants.Count == 0)
                throw new ApiExceptionResponse("File project has no active team.", 400);

            var teamGroupIds = new HashSet<Guid>();

            var filePermissions = await _unitOfWork.Repository<FilePermission>().FindAsync(
                p => p.FileItemId == fileItem.Id
                     && p.ProjectParticipantId.HasValue
                     && (!requireApprovePermission || p.CanApprove));
            foreach (var permission in filePermissions)
            {
                if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
                    teamGroupIds.Add(groupId);
            }

            var allFolders = (await _unitOfWork.Repository<Folder>().FindAsync(
                    f => f.ProjectId == folder.ProjectId))
                .ToDictionary(f => f.Id);

            if (!allFolders.TryGetValue(fileItem.FolderId, out var current))
                throw new ApiExceptionResponse("File folder not found.", 404);

            var folderIds = new HashSet<Guid>();
            while (folderIds.Add(current.Id) && current.ParentFolderId.HasValue
                   && allFolders.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                current = parent;
            }

            //var folderPermissions = await _unitOfWork.Repository<FolderPermissionServiceOld>().FindAsync(
            //    p => folderIds.Contains(p.FolderId)
            //         && p.ProjectParticipantId.HasValue
            //         && (!requireApprovePermission || p.CanApprove));
            //foreach (var permission in folderPermissions)
            //{
            //    if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
            //        teamGroupIds.Add(groupId);
            //}

            return teamGroupIds.Count > 0
                ? teamGroupIds
                : activeParticipants.Values.ToHashSet();
        }

        /// <summary>Overload khi chưa có folder sẵn — tự fetch rồi gọi overload chính.</summary>
        private async Task<IReadOnlyCollection<Guid>> ResolveFileItemTeamGroupIdsAsync(
            FileItem fileItem,
            bool requireApprovePermission)
        {
            var folder = await GetFolderAsync(fileItem.FolderId);
            return await ResolveFileItemTeamGroupIdsAsync(fileItem, folder, requireApprovePermission);
        }

        #endregion

        #region Tạo response

        /// <summary>Lọc danh sách request chỉ giữ lại những request actor có quyền xem.</summary>
        private async Task<IEnumerable<ApprovalRequestResponseDTO>> FilterVisibleRequestsAsync(
            IEnumerable<ApprovalRequest> requests,
            Guid actor)
        {
            var result = new List<ApprovalRequestResponseDTO>();
            var accounts = await GetAccountsByIdAsync();

            foreach (var request in requests)
            {
                try
                {
                    var fileItem = await GetFileItemAsync(request.FileItemId);
                    if (await CanViewRequestAsync(actor, request, fileItem))
                    {
                        var folder = await GetFolderAsync(fileItem.FolderId);
                        result.Add(BuildResponse(request, fileItem, accounts, folder));
                    }
                }
                catch (ApiExceptionResponse ex) when (ex.StatusCode == 404)
                {
                    _logger.LogWarning(
                        ex,
                        "Skipping approval request {ApprovalRequestId} because file item {FileItemId} was not found.",
                        request.Id,
                        request.FileItemId);
                }
            }

            return result;
        }

        private async Task<ApprovalRequestResponseDTO> BuildResponseAsync(ApprovalRequest request, FileItem? fileItem = null)
        {
            fileItem ??= await GetFileItemAsync(request.FileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);
            var accounts = await GetAccountsByIdAsync();
            return BuildResponse(request, fileItem, accounts, folder);
        }

        private ApprovalRequestResponseDTO BuildResponse(
            ApprovalRequest request,
            FileItem fileItem,
            IReadOnlyDictionary<Guid, Account> accounts,
            Folder folder)
        {
            accounts.TryGetValue(request.RequestedBy, out var requester);
            Account? approver = null;
            if (request.ApproverId.HasValue)
                accounts.TryGetValue(request.ApproverId.Value, out approver);

            return new ApprovalRequestResponseDTO
            {
                Id = request.Id,
                FileItemId = request.FileItemId,
                FileItemName = fileItem.Name,
                CurrentZone = _zoneResolver.FormatZone(folder.Area),
                TargetZone = GetTargetApprovalZone(folder.Area),
                RequiresSignature = fileItem.RequiresSignature,
                IsSigned = fileItem.IsSigned,
                RequestedBy = request.RequestedBy,
                RequestedByName = requester?.UserName,
                ApproverId = request.ApproverId,
                ApproverName = approver?.UserName,
                Status = request.Status,
                RejectReason = request.RejectReason,
                CreatedAt = request.CreatedAt,
                ApprovedAt = request.ApprovedAt
            };
        }

        private string? GetTargetApprovalZone(CdeArea currentZone)
            => currentZone == CdeArea.Archived
                ? null
                : _zoneResolver.FormatZone(GetNextApprovalZone(currentZone));

        private async Task<Dictionary<Guid, Account>> GetAccountsByIdAsync()
            => (await _unitOfWork.Repository<Account>().GetAllAsync()).ToDictionary(a => a.Id);

        #endregion
    }
}
