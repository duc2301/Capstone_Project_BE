using Application.DTOs.RequestDTOs.Approval;
using Application.DTOs.ResponseDTOs.Approval;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.File;
using Domain.Enum.Group;
using Domain.Enum.Project;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// Cài đặt nghiệp vụ phê duyệt file CDE.
    /// </summary>
    /// <remarks>
    /// Quy tắc chính:
    /// - Chỉ member active trong team của file mới được gửi duyệt.
    /// - Chỉ Team Leader active mới được duyệt hoặc từ chối.
    /// - Chỉ request Pending và file PendingApproval mới được xử lý.
    /// - Reject bắt buộc có lý do.
    /// </remarks>
    public class ApprovalService : IApprovalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ApprovalService> _logger;

        public ApprovalService(
            IUnitOfWork unitOfWork,
            ILogger<ApprovalService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region API chính

        public async Task<ApprovalRequestResponseDTO> SubmitAsync(Guid fileItemId, Guid actor)
        {
            var fileItem = await GetFileItemAsync(fileItemId);
            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: false);

            // 1. Kiểm tra quyền gửi duyệt.
            await RequireGroupMemberAsync(actor, teamGroupIds);

            // 2. Không cho tạo thêm request nếu file đã có request pending.
            var hasPendingRequest = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.FileItemId == fileItem.Id && a.Status == ApprovalRequestStatus.Pending))
                .Any();
            if (hasPendingRequest || fileItem.Status == FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("File is already pending approval.", 409);

            // 3. Tạo request và chuyển trạng thái file sang PendingApproval.
            var now = DateTime.UtcNow;
            var request = new ApprovalRequest
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItem.Id,
                RequestedBy = actor,
                Status = ApprovalRequestStatus.Pending,
                CreatedAt = now
            };

            fileItem.Status = FileItemStatus.PendingApproval;
            fileItem.UpdatedAt = now;

            await _unitOfWork.Repository<ApprovalRequest>().CreateAsync(request);
            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(request, fileItem);
        }

        public async Task<IEnumerable<ApprovalRequestResponseDTO>> GetAllAsync(Guid actor)
        {
            var requests = (await _unitOfWork.Repository<ApprovalRequest>().GetAllAsync())
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return await FilterVisibleRequestsAsync(requests, actor);
        }

        public async Task<IEnumerable<ApprovalRequestResponseDTO>> GetPendingAsync(Guid actor)
        {
            var pendingRequests = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.Status == ApprovalRequestStatus.Pending))
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return await FilterVisibleRequestsAsync(pendingRequests, actor);
        }

        public async Task<ApprovalRequestResponseDTO> GetByIdAsync(Guid id, Guid actor)
        {
            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);

            if (!await CanViewRequestAsync(actor, request, fileItem))
                throw new ApiExceptionResponse("You do not have permission to view this approval request.", 403);

            return await BuildResponseAsync(request, fileItem);
        }

        public async Task<ApprovalRequestResponseDTO> ApproveAsync(Guid id, Guid actor)
        {
            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);

            // Chỉ Team Leader được xử lý request đang Pending.
            await RequireCanDecideAsync(actor, request, fileItem);

            var now = DateTime.UtcNow;
            request.Status = ApprovalRequestStatus.Approved;
            request.ApproverId = actor;
            request.ApprovedAt = now;
            request.RejectReason = null;

            fileItem.Status = FileItemStatus.Approved;
            fileItem.UpdatedAt = now;

            await _unitOfWork.CommitAsync();
            return await BuildResponseAsync(request, fileItem);
        }

        public async Task<ApprovalRequestResponseDTO> RejectAsync(Guid id, RejectApprovalRequestDTO dto, Guid actor)
        {
            var reason = dto.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                throw new ApiExceptionResponse("Reject reason is required.", 400);

            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            await RequireCanDecideAsync(actor, request, fileItem);

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

        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        private async Task<ApprovalRequest> GetRequestAsync(Guid id)
            => await _unitOfWork.Repository<ApprovalRequest>().GetByIdAsync(id)
               ?? throw new ApiExceptionResponse("Approval request not found.", 404);

        #endregion

        #region Kiểm tra quyền

        private async Task RequireCanDecideAsync(Guid actor, ApprovalRequest request, FileItem fileItem)
        {
            if (request.Status != ApprovalRequestStatus.Pending || fileItem.Status != FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("Only pending approval requests can be approved or rejected.", 409);

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: true);
            if (!await IsGroupLeaderAsync(actor, teamGroupIds))
                throw new ApiExceptionResponse("Only the Team Leader can approve or reject this file.", 403);
        }

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

        private async Task<bool> IsGroupLeaderAsync(Guid accountId, IReadOnlyCollection<Guid> groupIds)
            => (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.AccountId == accountId
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Any();

        private async Task<bool> CanViewRequestAsync(Guid actor, ApprovalRequest request, FileItem fileItem)
        {
            if (request.RequestedBy == actor)
                return true;

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: true);
            return await IsGroupLeaderAsync(actor, teamGroupIds);
        }

        #endregion

        #region Xác định team của file

        private async Task<IReadOnlyCollection<Guid>> ResolveFileItemTeamGroupIdsAsync(
            FileItem fileItem,
            bool requireApprovePermission)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

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

            var folders = (await _unitOfWork.Repository<Folder>().FindAsync(
                    f => f.ProjectId == folder.ProjectId))
                .ToList();
            var byId = folders.ToDictionary(f => f.Id);

            if (!byId.TryGetValue(fileItem.FolderId, out var current))
                throw new ApiExceptionResponse("File folder not found.", 404);

            var folderIds = new HashSet<Guid>();
            while (folderIds.Add(current.Id) && current.ParentFolderId.HasValue
                   && byId.TryGetValue(current.ParentFolderId.Value, out var parent))
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

        #endregion

        #region Tạo response

        private async Task<IEnumerable<ApprovalRequestResponseDTO>> FilterVisibleRequestsAsync(
            IEnumerable<ApprovalRequest> requests, Guid actor)
        {
            var result = new List<ApprovalRequestResponseDTO>();
            var accounts = await GetAccountsByIdAsync();

            foreach (var request in requests)
            {
                try
                {
                    var fileItem = await GetFileItemAsync(request.FileItemId);
                    if (await CanViewRequestAsync(actor, request, fileItem))
                        result.Add(BuildResponse(request, fileItem, accounts));
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
            var accounts = await GetAccountsByIdAsync();
            return BuildResponse(request, fileItem, accounts);
        }

        private static ApprovalRequestResponseDTO BuildResponse(
            ApprovalRequest request,
            FileItem fileItem,
            IReadOnlyDictionary<Guid, Account> accounts)
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

        private async Task<Dictionary<Guid, Account>> GetAccountsByIdAsync()
            => (await _unitOfWork.Repository<Account>().GetAllAsync()).ToDictionary(a => a.Id);

        #endregion
    }
}
